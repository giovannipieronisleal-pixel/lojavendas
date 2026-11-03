using lojavendas.Models;
using SQLite;

namespace lojavendas.Services;

public sealed class CommerceService
{
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _backupLock = new(1, 1);
    private bool _isInitialized;
    private string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, "lojavendas.db3");
    private string BackupDirectory => Path.Combine(FileSystem.AppDataDirectory, "Backups");

    public CommerceService()
    {
        _database = new SQLiteAsyncConnection(DatabasePath);
    }

    public async Task<IReadOnlyList<ProductRecord>> GetProductsAsync(bool availableOnly = false)
    {
        await EnsureInitializedAsync();
        var query = _database.Table<ProductRecord>();
        if (availableOnly)
            query = query.Where(product => product.Stock > 0);

        return await query.OrderBy(product => product.Name).ToListAsync();
    }

    public async Task CreateProductAsync(ProductInput input)
    {
        await EnsureInitializedAsync();
        ValidateProduct(input);

        var product = new ProductRecord
        {
            Name = input.Name.Trim(),
            Code = input.Code.Trim(),
            Category = input.Category.Trim(),
            Price = input.Price,
            Stock = input.Stock
        };

        try
        {
            await _database.InsertAsync(product);
        }
        catch (SQLiteException exception) when (exception.Result == SQLite3.Result.Constraint)
        {
            throw new InvalidOperationException("Já existe um produto com esse código.", exception);
        }
    }

    public async Task<IReadOnlyList<SaleRecord>> GetSalesAsync(bool debtsOnly = false)
    {
        await EnsureInitializedAsync();
        var query = _database.Table<SaleRecord>();
        if (debtsOnly)
            query = query.Where(sale => sale.Status == "Pendente");

        return await query.OrderByDescending(sale => sale.CreatedAt).ToListAsync();
    }

    public async Task<IReadOnlyList<SaleRecord>> GetCreditSalesAsync()
    {
        await EnsureInitializedAsync();
        return await _database.Table<SaleRecord>()
            .Where(sale => sale.PaymentMethod == "Pagar depois")
            .OrderByDescending(sale => sale.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetLatestDebtPaymentMethodsAsync()
    {
        await EnsureInitializedAsync();
        var payments = await _database.Table<SalePaymentRecord>()
            .OrderByDescending(payment => payment.CreatedAt)
            .ToListAsync();

        return payments
            .GroupBy(payment => payment.SaleId)
            .ToDictionary(group => group.Key, group => group.First().PaymentMethod);
    }

    public async Task<IReadOnlyList<SalePaymentRecord>> GetSalePaymentsAsync(int saleId)
    {
        await EnsureInitializedAsync();
        return await _database.Table<SalePaymentRecord>()
            .Where(payment => payment.SaleId == saleId)
            .OrderByDescending(payment => payment.CreatedAt)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<SaleRecord> Sales, IReadOnlyList<SaleItemRecord> Items, IReadOnlyList<SalePaymentRecord> Payments)> GetAnalyticsDataAsync()
    {
        await EnsureInitializedAsync();
        var sales = await _database.Table<SaleRecord>().ToListAsync();
        var items = await _database.Table<SaleItemRecord>().ToListAsync();
        var payments = await _database.Table<SalePaymentRecord>().ToListAsync();
        return (sales, items, payments);
    }

    public async Task<string?> CreateDailyBackupIfDueAsync()
    {
        await EnsureInitializedAsync();
        await _backupLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            var backupPath = Path.Combine(
                BackupDirectory,
                $"lojavendas-{DateTime.Now:yyyy-MM-dd}.db3");
            if (!File.Exists(backupPath))
                await CreateDatabaseSnapshotAsync(backupPath);

            RemoveExpiredBackups();
            return backupPath;
        }
        finally
        {
            _backupLock.Release();
        }
    }

    public async Task<string> CreateManualBackupAsync()
    {
        await EnsureInitializedAsync();
        await _backupLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            var backupPath = Path.Combine(
                BackupDirectory,
                $"lojavendas-manual-{DateTime.Now:yyyy-MM-dd-HHmmss}.db3");
            await CreateDatabaseSnapshotAsync(backupPath);
            RemoveExpiredBackups();
            return backupPath;
        }
        finally
        {
            _backupLock.Release();
        }
    }

    public string? GetLatestBackupPath()
    {
        if (!Directory.Exists(BackupDirectory))
            return null;

        return Directory.GetFiles(BackupDirectory, "*.db3")
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
    }

    public async Task<SalePaymentRecord> RegisterDebtPaymentAsync(int saleId, decimal amount, string paymentMethod)
    {
        await EnsureInitializedAsync();
        if (amount <= 0)
            throw new ArgumentException("O valor do pagamento deve ser maior que zero.");
        if (paymentMethod is not ("Dinheiro" or "Pix" or "Cartão"))
            throw new ArgumentException("Selecione uma forma de pagamento válida.");

        SalePaymentRecord? createdPayment = null;
        await _database.RunInTransactionAsync(connection =>
        {
            var sale = connection.Find<SaleRecord>(saleId)
                ?? throw new InvalidOperationException("A venda a prazo não foi encontrada.");
            if (sale.Status != "Pendente")
                throw new InvalidOperationException("Esta dívida já foi quitada.");

            var amountInCents = decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
            if (amountInCents <= 0)
                throw new ArgumentException("O valor do pagamento deve ser de pelo menos R$ 0,01.");
            var outstandingInCents = sale.TotalInCents - sale.AmountPaidInCents;
            if (amountInCents > outstandingInCents)
                throw new ArgumentException("O pagamento não pode ser maior que o saldo devedor.");

            var totalPaidInCents = sale.AmountPaidInCents + amountInCents;
            var remainingInCents = sale.TotalInCents - totalPaidInCents;
            createdPayment = new SalePaymentRecord
            {
                SaleId = sale.Id,
                PaymentMethod = paymentMethod,
                AmountInCents = amountInCents,
                TotalPaidInCents = totalPaidInCents,
                OutstandingAfterInCents = remainingInCents,
                SaleTotalInCents = sale.TotalInCents,
                CreatedAt = DateTime.Now
            };
            connection.Insert(createdPayment);
            createdPayment.ReceiptNumber = $"PG-{createdPayment.Id:0000}";
            connection.Update(createdPayment);

            sale.AmountPaidInCents = totalPaidInCents;
            if (remainingInCents == 0)
                sale.Status = "Concluída";
            connection.Update(sale);
        });

        return createdPayment!;
    }

    public async Task<SaleRecord> CreateSaleAsync(SaleInput input, string sellerName)
    {
        await EnsureInitializedAsync();
        if (input.Items.Count == 0)
            throw new ArgumentException("Adicione pelo menos um produto à venda.");
        if (input.PaymentMethod == "Pagar depois" && string.IsNullOrWhiteSpace(input.CustomerName))
            throw new ArgumentException("Informe o nome do cliente para registrar a venda a prazo.");

        SaleRecord? createdSale = null;
        await _database.RunInTransactionAsync(connection =>
        {
            var itemsToInsert = new List<SaleItemRecord>();
            decimal total = 0;
            foreach (var requestedItem in input.Items)
            {
                if (requestedItem.Quantity <= 0)
                    throw new ArgumentException("A quantidade deve ser maior que zero.");

                var product = connection.Find<ProductRecord>(requestedItem.ProductId)
                    ?? throw new InvalidOperationException("Um produto da venda não foi encontrado.");
                if (product.Stock < requestedItem.Quantity)
                    throw new InvalidOperationException($"Estoque insuficiente para {product.Name}.");

                var subtotal = product.Price * requestedItem.Quantity;
                total += subtotal;
                product.Stock -= requestedItem.Quantity;
                connection.Update(product);

                itemsToInsert.Add(new SaleItemRecord
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = requestedItem.Quantity,
                    Subtotal = subtotal
                });
            }

            var isPayLater = input.PaymentMethod == "Pagar depois";
            var amountPaid = isPayLater ? 0 : input.AmountPaid;
            if (!isPayLater && amountPaid < total)
                throw new ArgumentException("O valor recebido não pode ser menor que o total da venda.");

            createdSale = new SaleRecord
            {
                CustomerName = input.CustomerName.Trim(),
                SellerName = sellerName,
                PaymentMethod = input.PaymentMethod,
                Status = isPayLater ? "Pendente" : "Concluída",
                Total = total,
                AmountPaid = amountPaid,
                Change = isPayLater ? 0 : amountPaid - total,
                CreatedAt = DateTime.Now
            };
            connection.Insert(createdSale);
            createdSale.SaleNumber = $"V-{createdSale.Id:0000}";
            connection.Update(createdSale);

            foreach (var item in itemsToInsert)
            {
                item.SaleId = createdSale.Id;
                connection.Insert(item);
            }
        });

        return createdSale!;
    }

    private static void ValidateProduct(ProductInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120)
            throw new ArgumentException("Informe o nome do produto com até 120 caracteres.");
        if (string.IsNullOrWhiteSpace(input.Code) || input.Code.Trim().Length > 64)
            throw new ArgumentException("Informe um código de produto com até 64 caracteres.");
        if (string.IsNullOrWhiteSpace(input.Category) || input.Category.Trim().Length > 80)
            throw new ArgumentException("Informe uma categoria com até 80 caracteres.");
        if (input.Price <= 0)
            throw new ArgumentException("O preço deve ser maior que zero.");
        if (input.Stock < 0)
            throw new ArgumentException("O estoque não pode ser negativo.");
    }

    private async Task CreateDatabaseSnapshotAsync(string destinationPath)
    {
        if (File.Exists(destinationPath))
            throw new IOException("Já existe um arquivo de backup com esse nome.");

        var escapedPath = destinationPath.Replace("'", "''", StringComparison.Ordinal);
        await _database.ExecuteAsync($"VACUUM INTO '{escapedPath}'");
    }

    private void RemoveExpiredBackups()
    {
        var expirationDate = DateTime.Now.AddDays(-30);
        foreach (var backupPath in Directory.GetFiles(BackupDirectory, "*.db3"))
        {
            if (File.GetLastWriteTime(backupPath) < expirationDate)
                File.Delete(backupPath);
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
            return;

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
            await _database.CreateTableAsync<ProductRecord>();
            await _database.CreateTableAsync<SaleRecord>();
            await _database.CreateTableAsync<SalePaymentRecord>();
            await _database.CreateTableAsync<SaleItemRecord>();
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
