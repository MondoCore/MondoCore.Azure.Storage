/**************************************************************************
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
using Microsoft.VisualBasic;
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
    public class AzureBlobStorageWriter<T>(AzureBlobStorage<T> store, AzureStorageRetryPolicy? retryPolicy = null) : BaseBlobStorageWriter<T>(store, retryPolicy)
    {
        /// <inheritdoc/>
        protected internal override Task<Stream> OpenWrite(BlobBaseClient client, string id, string? leaseId, CancellationToken cancellationToken)
        {
            var options = new BlobOpenWriteOptions { };

            if(leaseId != null)
                options.OpenConditions = new BlobRequestConditions { LeaseId = leaseId };

            return (client as BlobClient)!.OpenWriteAsync(true, options, cancellationToken: cancellationToken);
        }

        public override async Task<IBlobLease> AcquireLease(string id, bool createIfNotExists = true, CancellationToken cancellationToken = default)
        {
            var  lease = new BlobLease<T>(store, this);

            await lease.Acquire(id, createIfNotExists, cancellationToken, false);

            return lease;
        }

        #region Private

        internal override async Task CreateIfNotExists(BlobBaseClient blob, string? leaseId, CancellationToken cancellationToken)
        {
            if(await blob.ExistsAsync(cancellationToken))
                return;

            using var stream = new MemoryStream();

            await Put(()=> Task.FromResult((blob!, (string?)null)), stream, cancellationToken: cancellationToken);
        }

        internal override async Task Put(Func<Task<(BlobBaseClient Client, string? LeaseId)>> getBlob, Stream contents, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob = await getBlob();
                var blobClient = blob.Client as BlobClient;

                if(blob.LeaseId != null)
                {
                    var options = new BlobUploadOptions { Conditions = new() { LeaseId = blob.LeaseId} };

                    await blobClient!.UploadAsync(contents, options, cancellationToken).ConfigureAwait(false);
                }
                else            
                    await blobClient!.UploadAsync(contents, true, cancellationToken).ConfigureAwait(false);
            }
            catch(RequestFailedException ex) when (ex.Status == 404)
            { 
                throw new FileNotFoundException("Blob not found", ex);
            }
            catch(RequestFailedException ex) when (ex.Status == 401)
            { 
                throw new UnauthorizedAccessException("Blob not accessible", ex);
            }
            catch(RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
            {
                throw new LeaseException(ex);
            }
        }

        #endregion    
    }
}
