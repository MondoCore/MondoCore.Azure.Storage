/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: BaseBlobStorageWriter_T.cs			 		    		    
 *        Class(es): BaseBlobStorageWriter<T>				           		        
 *          Purpose: Base class for Azure blob storage writer                          
 *                                                                          
 *  Original Author: Jim Lightfoot                                          
 *    Creation Date: 4 Jan 2026                                             
 *                                                                          
 *   Copyright (c) 2026 - Jim Lightfoot, All rights reserved                
 *                                                                          
 *  Licensed under the MIT license:                                         
 *    http://www.opensource.org/licenses/mit-license.php                    
 *                                                                          
 ****************************************************************************/

using System;
using System.IO;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;

using Azure;
using Azure.Storage.Blobs.Specialized;

using MondoCore.Common;

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Base class for Azure blob storage writer 
    /// </summary>
    public abstract class BaseBlobStorageWriter<T>(BaseBlobStorage<T> store, AzureStorageRetryPolicy? retryPolicy) : IBlobStoreWriter<T>
    {
        #region IBlobStoreWriter<T>

        /// <inheritdoc/>
        public Task<Stream> OpenWrite(string id, CancellationToken cancellationToken = default)
        {
            return OpenWriteInternal(async ()=> (await store.GetBlobClient(id).ConfigureAwait(false), null), id, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task Put(string id, Stream contents, CancellationToken cancellationToken = default)
        {
            var blob = await store.GetBlobClient(id).ConfigureAwait(false);

            await Put(()=> Task.FromResult((blob!, (string?)null)), contents, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public Task Delete(string id, CancellationToken cancellationToken = default)
        {
            return Delete(async ()=> (await store.GetBlobClient(id).ConfigureAwait(false), null), cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async Task<IBlobLease> AcquireLease(string id, bool createIfNotExists = true, CancellationToken cancellationToken = default)
        {
            var  lease = new BlobLease<T>(store, this);
            int  retry = retryPolicy?.MaxRetries ?? 0;

            while(true)
            { 
                try
                { 
                    await lease.Acquire(id, createIfNotExists, cancellationToken);
                    return lease;
                }
                catch(RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
                { 
                    if(retryPolicy == null || retryPolicy!.MaxRetries == 0)
                        throw new LeaseException(ex);

                    if(retry-- <= 0)
                        throw new LeaseException(ex);

                    await Task.Delay(retryPolicy.Delay);
                }
            }
        }

        #endregion

        #region Protected

        protected internal abstract Task<Stream> OpenWrite(BlobBaseClient client, string id, string? leaseId, CancellationToken cancellationToken);

        #endregion

        #region Private

        internal abstract Task Put(Func<Task<(BlobBaseClient Client, string? LeaseId)>> getBlob, Stream contents, CancellationToken cancellationToken = default);
        
        internal async Task<Stream> OpenWriteInternal(Func<Task<(BlobBaseClient Client, string? LeaseId)>> getBlob, string id, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob = await getBlob();

                return await OpenWrite(blob.Client, id, blob.LeaseId, cancellationToken).ConfigureAwait(false);
            }
            catch(RequestFailedException ex) when (ex.Status == 404)
            {
                throw new FileNotFoundException("Blob not found", ex);
            }
            catch(RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
            {
                throw new LeaseException(ex);
            }
        }

        internal async Task Delete(Func<Task<(BlobBaseClient Client, string? LeaseId)>> getBlob, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob = await getBlob();

                await blob.Client.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch(RequestFailedException ex) when (ex.Status == 404)
            { 
                // Do nothing if blob doesn't exist
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

        internal virtual Task CreateIfNotExists(BlobBaseClient blob, string? leaseId, CancellationToken cancellationToken)
        {
            // Default implementation is to do nothing
            return Task.CompletedTask;
        }

        #endregion   
    }
}
