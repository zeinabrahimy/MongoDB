namespace Database
{
	public interface INoSqlEntityDatabaseInfo<T> where T : class
	{
		string EntityConnectionString { get; }
		string DatabaseName { get; }
	}
}
