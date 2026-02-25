namespace LittleBit.Modules.IAppModule.Data.ProductWrappers
{
    public class ProductWrapper : IProductWrapper
    {
        public TransactionData TransactionData { get; protected set; }
        public Metadata Metadata { get; protected set; }
        public Definition Definition { get; protected set; }
        
    }
}