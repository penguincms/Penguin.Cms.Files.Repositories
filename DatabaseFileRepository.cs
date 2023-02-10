using Penguin.Cms.Repositories;
using Penguin.Files.Services;
using Penguin.Messaging.Core;
using Penguin.Messaging.Persistence.Messages;
using Penguin.Persistence.Abstractions.Interfaces;
using Penguin.Security.Abstractions.Extensions;
using Penguin.Security.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;

using System.Linq;

namespace Penguin.Cms.Files.Repositories
{
    public class DatabaseFileRepository : AuditableEntityRepository<DatabaseFile>
    {
        protected FileService FileService { get; set; }

        protected ISecurityProvider<DatabaseFile> SecurityProvider { get; set; }

        public DatabaseFileRepository(IPersistenceContext<DatabaseFile> dbContext, FileService fileService, ISecurityProvider<DatabaseFile> securityProvider = null, MessageBus messageBus = null) : base(dbContext, messageBus)
        {
            SecurityProvider = securityProvider;
            FileService = fileService;
        }

        public override void AcceptMessage(Updating<DatabaseFile> updateMessage)
        {
            if (updateMessage is null)
            {
                throw new ArgumentNullException(nameof(updateMessage));
            }

            Process(updateMessage.Target);
            base.AcceptMessage(updateMessage);
        }

        public override void Delete(DatabaseFile o)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            DeleteFile(o);

            base.Delete(o);
        }

        public override void DeleteRange(IEnumerable<DatabaseFile> o)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            foreach (DatabaseFile entity in o)
            {
                DeleteFile(entity);
            }

            base.DeleteRange(o);
        }

        public override DatabaseFile Find(Guid guid)
        {
            return SecurityProvider.TryFind(base.Find(guid));
        }

        public DatabaseFile GetByFullName(string FullName)
        {
            if (FullName is null)
            {
                throw new ArgumentNullException(nameof(FullName));
            }

            if (Path.DirectorySeparatorChar == '\\')
            {
                FullName = FullName.Replace('/', '\\');
            }

            //EF is dumb as fuck
            string dirChar = Path.DirectorySeparatorChar.ToString();

            DatabaseFile db = this.Where(f => f.FilePath + dirChar + f.FileName == FullName).OrderByDescending(f => f._Id).FirstOrDefault();

            return db;
        }

        public List<DatabaseFile> GetByOwner(Guid OwnerGuid)
        {
            return this.Where(f => f.Owner == OwnerGuid).ToList();
        }

        public List<DatabaseFile> GetByPath(string FilePath, bool Recursive = false)
        {
            List<DatabaseFile> toReturn = new();

            int i = 0;

            toReturn.AddRange(this.Where(f => f.FilePath == FilePath).ToList().Where(d => SecurityProvider.TryCheckAccess(d)).ToList());

            if (Recursive)
            {
                while (i < toReturn.Count)
                {
                    if (toReturn[i].IsDirectory)
                    {
                        toReturn.AddRange(GetByPath(toReturn[i].FullName));
                    }

                    i++;
                }
            }

            return toReturn;
        }

        private static void DeleteFile(DatabaseFile o)
        {
            o.Data = Array.Empty<byte>();

            if (File.Exists(o.FullName))
            {
                File.Delete(o.FullName);
            }
        }

        private void Process(params DatabaseFile[] o)
        {
            foreach (DatabaseFile entity in o)
            {
                FileService.StoreOnDisk(entity);

                entity.ExternalId = entity.FullName;
            }
        }
    }
}