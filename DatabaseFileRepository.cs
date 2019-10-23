using Penguin.Cms.Repositories;
using Penguin.Files.Services;
using Penguin.Messaging.Core;
using Penguin.Messaging.Persistence.Messages;
using Penguin.Persistence.Abstractions.Interfaces;
using Penguin.Security.Abstractions.Extensions;
using Penguin.Security.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;

using System.Linq;

namespace Penguin.Cms.Files.Repositories
{
    [SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix")]
    public class DatabaseFileRepository : AuditableEntityRepository<DatabaseFile>
    {
        protected FileService FileService { get; set; }
        protected ISecurityProvider<DatabaseFile> SecurityProvider { get; set; }

        public DatabaseFileRepository(IPersistenceContext<DatabaseFile> dbContext, FileService fileService, ISecurityProvider<DatabaseFile> securityProvider = null, MessageBus messageBus = null) : base(dbContext, messageBus)
        {
            SecurityProvider = securityProvider;
            this.FileService = fileService;
        }

        public override void AcceptMessage(Updating<DatabaseFile> update)
        {
            if (update is null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            this.Process(update.Target);
            base.AcceptMessage(update);
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

        public override DatabaseFile Find(Guid guid) => SecurityProvider.TryFind(base.Find(guid));

        public DatabaseFile GetByFullName(string FullName)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(FullName));
            FullName = FullName.Replace('/', '\\');

            DatabaseFile db = this.Where(f => f.FilePath + "\\" + f.FileName == FullName).OrderByDescending(f => f._Id).FirstOrDefault();

            return db;
        }

        public List<DatabaseFile> GetByOwner(Guid OwnerGuid) => this.Where(f => f.Owner == OwnerGuid).ToList();

        public List<DatabaseFile> GetByPath(string FilePath, bool Recursive = false)
        {
            List<DatabaseFile> toReturn = new List<DatabaseFile>();

            int i = 0;

            toReturn.AddRange(this.Where(f => f.FilePath == FilePath).ToList().Where(d => SecurityProvider.TryCheckAccess(d)).ToList());

            if (Recursive)
            {
                while (i < toReturn.Count)
                {
                    if (toReturn[i].IsDirectory)
                    {
                        toReturn.AddRange(this.GetByPath(toReturn[i].FullName));
                    }

                    i++;
                }
            }

            return toReturn;
        }

        private void DeleteFile(DatabaseFile o)
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
                this.FileService.StoreOnDisk(entity);

                entity.ExternalId = entity.FullName;
            }
        }
    }
}