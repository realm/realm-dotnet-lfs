using System.IO;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace Realms.LFS
{
    public partial class FileData : IRealmObject
    {
        [PrimaryKey]
        public ObjectId Id { get; private set; } = ObjectId.GenerateNewId();

        public Task<Stream> GetStream() => FileManager.ReadFile(this);

        private int StatusInt { get; set; }

        public DataStatus Status
        {
            get => (DataStatus)StatusInt;
            internal set => StatusInt = (int)value;
        }

        public string LocalUrl => FileManager.GetFilePath(this);

        public string Url { get; internal set; }

        public string Name { get; private set; }

        public FileData(Stream data, string name = null)
        {
            FileManager.WriteFile(FileLocation.Temporary, Id, data);
            Name = name;
            Status = DataStatus.Local;
        }

        partial void OnManaged()
        {
            if (Status == DataStatus.Local)
            {
                // TODO: That's not very efficient - it checks for file existence
                // on every instantiation - we should be able to do it more efficiently 
                FileManager.UploadFile(FileLocation.Temporary, this);
            }
        }
    }
}
