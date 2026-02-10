/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: AzurePageBlobStorage_T.cs			 		    		    
 *        Class(es): AzurePageBlobStorage <T>			           		        
 *          Purpose: Class for page blob storage in Azure Storage                           
 *                                                                          
 *  Original Author: Jim Lightfoot                                          
 *    Creation Date: 4 Feb 2026                                             
 *                                                                          
 *   Copyright (c) 2026 - Jim Lightfoot, All rights reserved                
 *                                                                          *                                                                          
 *  Licensed under the MIT license:                                         
 *    http://www.opensource.org/licenses/mit-license.php                    
 *                                                                          
 ****************************************************************************/

using System;
using System.IO;
using System.Threading.Tasks;

using Azure.Core;
using Azure.Storage.Blobs.Specialized;

using MondoCore.Common;

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Azure page blob storage
    /// </summary>
    public class AzurePageBlobStorage<T> : BaseBlobStorage<T>
    {
        public AzurePageBlobStorage(string connectionString, string blobContainerName) : base(connectionString, blobContainerName)
        {
        }

        public AzurePageBlobStorage(Uri uri, TokenCredential credential, string path) : base(uri, credential, path)
        {
        }

        #region BaseBlobStorage

        public override IBlobStoreReader<T> Reader => new AzurePageBlobStorageReader<T>(this);

        /// <inheritdoc/>
        public override IBlobStoreWriter<T> Writer => new AzurePageBlobStorageWriter<T>(this);

        /// <inheritdoc/>
        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }        

        #endregion

        #region Internal

        internal override async Task<BlobBaseClient> GetBlobClient(string blobName, bool createIfNotExists = false)
        {
            PageBlobClient? blob = null;
            
            if(this.Uri != null)
            { 
                var pageUri =  this.Uri.Combine(this.FolderName!, blobName);
                
                blob = new PageBlobClient(pageUri, this.Credential);
            }
            else
                blob = new PageBlobClient(this.ConnectionString, this.ContainerName, Path.Combine(this.FolderName!, blobName));

            if(createIfNotExists)
                await blob.CreateIfNotExistsAsync(1024).ConfigureAwait(false);

            return blob;
        }

        #endregion
    }
}
