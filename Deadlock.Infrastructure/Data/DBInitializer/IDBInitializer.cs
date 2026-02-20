using System.Threading.Tasks;

namespace Deadlock.Infrastructure.Data.DBInitializer
{
    public interface IDBInitializer
    {
         Task Initialize();
    }
}
