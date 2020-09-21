using System.Transactions;

namespace Database.Transactional
{
	internal class MongoDBResourceManager : IEnlistmentNotification
	{
		MongoDB.Driver.IClientSessionHandle clientSessionHandle;
		public MongoDBResourceManager(MongoDB.Driver.IClientSessionHandle clientSessionHandle)
		{
			this.clientSessionHandle = clientSessionHandle;
		}
		public void Commit(Enlistment enlistment)
		{
			if (clientSessionHandle.IsInTransaction)
				clientSessionHandle.CommitTransaction();

			enlistment.Done();
		}

		public void InDoubt(Enlistment enlistment)
		{
			enlistment.Done();
		}

		public void Prepare(PreparingEnlistment preparingEnlistment)
		{

			preparingEnlistment.Prepared();
		}

		public void Rollback(Enlistment enlistment)
		{
			if (clientSessionHandle.IsInTransaction)
				clientSessionHandle.AbortTransaction();
			enlistment.Done();
		}
	}
}
