namespace ward.Constants
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Cashier = "Cashier";
        public const string InventoryClerk = "InventoryClerk";
    }

    public static class AppPolicies
    {
        public const string RequireAdmin = "RequireAdmin";
        public const string ManageCatalog = "ManageCatalog";
        public const string ManageInventory = "ManageInventory";
        public const string ProcessSales = "ProcessSales";
        public const string ProcessReturns = "ProcessReturns";
        public const string ViewAuditAndReports = "ViewAuditAndReports";
    }
}
