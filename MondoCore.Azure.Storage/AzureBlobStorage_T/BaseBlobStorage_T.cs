/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: BaseBlobStorage.cs			 		    		    
 *        Class(es): BaseBlobStorage				           		        
 *          Purpose: Base class for blob storage accounts in Azure Storge                           
 *                                                                          
 *  Original Author: Jim Lightfoot                                          
 *    Creation Date: 3 Feb 2026                                             
 *                                                                          
 *   Copyright (c) 2026 - Jim Lightfoot, All rights reserved                
 *                                                                          
 *  Licensed under the MIT license:                                         
 *    http://www.opensource.org/licenses/mit-license.php                    
 *                                                                          
 ****************************************************************************/

using System;
using System.Linq;
using System.Threading.Tasks;

using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

using MondoCore.Common;

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Base class for Azure blob stores
    /// </summary>
    public abstract class BaseBlobStorage<T> : IBlobStore<T>
    {
        protected BaseBlobStorage(string connectionString, string blobContainerName)
        {
            var folderParts = blobContainerName.Split('/');

            this.ContainerName    = folderParts[0];
            this.ConnectionString = connectionString;

            if(folderParts.Length > 1)
                this.FolderName = string.Join("/", folderParts.Skip(1)).EnsureEndsWith("/");
            else
                this.FolderName = "";
        }

        protected BaseBlobStorage(Uri uri, TokenCredential credential, string path)
        {
            this.Uri        = uri;
            this.Credential = credential!;

            if(!string.IsNullOrWhiteSpace(path))
                this.FolderName = path.EnsureEndsWith("/");
        }

        #region IBlobStore

        /// <inheritdoc/>
        public abstract IBlobStoreReader<T> Reader { get; }

        /// <inheritdoc/>
        public abstract IBlobStoreWriter<T> Writer { get; }

        /// <inheritdoc/>
        public abstract ValueTask DisposeAsync();

        #endregion

        internal string?          ConnectionString  { get; }
        internal string?          ContainerName     { get; }
        internal string?          FolderName        { get; }
        internal Uri?             Uri               { get; }
        internal TokenCredential? Credential        { get; }

        #region Internal

        internal BlobContainerClient ContainerClient
        {
            get
            { 
                if(this.Uri != null)
                    return new BlobContainerClient(this.Uri, this.Credential);

                return new BlobContainerClient(this.ConnectionString, this.ContainerName);
            }
        }

        internal abstract Task<BlobBaseClient> GetBlobClient(string blobName, bool createIfNotExists = false);

        #endregion
    }
}
