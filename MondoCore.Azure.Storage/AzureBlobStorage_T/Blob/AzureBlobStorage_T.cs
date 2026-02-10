/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: AzureStorage.cs			 		    		    
 *        Class(es): AzureStorage				           		        
 *          Purpose: Class for blob storage in Azure Storage                           
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
using System.IO;
using System.Threading.Tasks;

using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

using MondoCore.Common;

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Class to access Azure blob storage
    /// </summary>
    public class AzureBlobStorage<T> : BaseBlobStorage<T>
    {
        public AzureBlobStorage(string connectionString, string blobContainerName) : base(connectionString, blobContainerName)
        {
        }

        public AzureBlobStorage(Uri uri, TokenCredential credential, string path) : base(uri, credential, path)
        {
        }

        #region BaseBlobStorage

        public override IBlobStoreReader<T> Reader => new AzureBlobStorageReader<T>(this);

        /// <inheritdoc/>
        public override IBlobStoreWriter<T> Writer => new AzureBlobStorageWriter<T>(this);

        /// <inheritdoc/>
        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        #endregion
       
        #region Internal

        internal override Task<BlobBaseClient> GetBlobClient(string blobName, bool createIfNotExists = false)
        { 
            BlobClient? blob = null;

            if(this.Uri != null)
            { 
                var pageUri =  this.Uri.Combine(this.FolderName!, blobName);

                blob = new BlobClient(pageUri, this.Credential);
            }
            else
                blob = new BlobClient(this.ConnectionString, this.ContainerName, Path.Combine(this.FolderName!, blobName));

            return Task.FromResult(blob as BlobBaseClient);
        }

        #endregion
    }
}
