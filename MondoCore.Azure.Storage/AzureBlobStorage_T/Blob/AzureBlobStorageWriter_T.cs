/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: AzureBlobStorageWriter_T.cs			 		    		    
 *        Class(es): AzureBlobStorageWriter<T>				           		        
 *          Purpose: Class to perform write operations on a Azure storage account                           
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

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using MondoCore.Common;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Class to perform write operations on an Azure storage account  
    /// </summary>
    public class AzureBlobStorageWriter<T>(AzureBlobStorage<T> store) : BaseBlobStorageWriter<T>(store)
    {
        /// <inheritdoc/>
        public override async Task Put(string id, Stream contents, CancellationToken cancellationToken = default)
        {
            var blob = (await store.GetBlobClient(id).ConfigureAwait(false)) as BlobClient;

            await Put(()=> Task.FromResult((blob!, (string?)null)), contents, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override Task Put(IBlobLease lease, Stream contents, CancellationToken cancellationToken = default)
        {
            var blobLease = lease as BlobLease<T>;

            return Put(()=> Task.FromResult(((blobLease!.BlobClient! as BlobClient)!, (string?)blobLease.LeaseId)), contents, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        protected internal override Task<Stream> OpenWrite(BlobBaseClient client, string? leaseId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Cannot open a write stream on this type of Azure Blob Storage. Use AzurePageBlobStorage.");
        }

        #region Private

        private async Task Put(Func<Task<(BlobClient Client, string? LeaseId)>> getBlob, Stream contents, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob = await getBlob();

                await blob.Client.UploadAsync(contents, cancellationToken).ConfigureAwait(false);
            }
            catch(RequestFailedException ex) when (ex.Status == 404)
            { 
                throw new FileNotFoundException("Blob not found", ex);
            }
            catch(RequestFailedException ex) when (ex.Status == 401)
            { 
                throw new UnauthorizedAccessException("Blob not accessible", ex);
            }
        }

        #endregion    
    }
}
