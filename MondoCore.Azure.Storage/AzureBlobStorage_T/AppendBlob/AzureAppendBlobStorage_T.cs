/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: AzureAppendBlobStorage_T.cs			 		    		    
 *        Class(es): AzureAppendBlobStorage<T>				           		        
 *          Purpose: Class for blob storage in Azure append blob storage                           
 *                                                                          
 *  Original Author: Jim Lightfoot                                          
 *    Creation Date: 4 Feb 2026                                             
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
using Azure.Storage.Blobs.Specialized;

using MondoCore.Common;

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Class to access Azure blob storage
    /// </summary>
    public class AzureAppendBlobStorage<T> : BaseBlobStorage<T>
    {
        public AzureAppendBlobStorage(string connectionString, string blobContainerName) : base(connectionString, blobContainerName)
        {
        }

        public AzureAppendBlobStorage(Uri uri, TokenCredential credential, string path) : base(uri, credential, path)
        {
        }

        #region BaseBlobStorage

        public override IBlobStoreReader<T> Reader => new AzureAppendBlobStorageReader<T>(this);

        /// <inheritdoc/>
        public override IBlobStoreWriter<T> Writer => new AzureAppendBlobStorageWriter<T>(this);

        /// <inheritdoc/>
        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        #endregion
       
        #region Internal

        internal override Task<BlobBaseClient> GetBlobClient(string blobName, bool createIfNotExists = false)
        { 
            BlobBaseClient? blob = null;

            if(this.Uri != null)
            { 
                var pageUri =  this.Uri.Combine(this.FolderName!, blobName);

                blob = new AppendBlobClient(pageUri, this.Credential);
            }
            else
                blob = new AppendBlobClient(this.ConnectionString, this.ContainerName, Path.Combine(this.FolderName!, blobName));

            return Task.FromResult(blob as BlobBaseClient);
        }

        #endregion
    }
}
