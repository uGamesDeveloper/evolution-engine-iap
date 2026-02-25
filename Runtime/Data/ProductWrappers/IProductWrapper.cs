using UnityEngine.Purchasing;

namespace LittleBit.Modules.IAppModule.Data.ProductWrappers
{
    public interface IProductWrapper
    {
        public TransactionData TransactionData { get; }
        
        public Metadata Metadata { get; }
        public Definition Definition { get; }

    }
}