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
    public abstract class BaseBlobStorageWriter<T>(BaseBlobStorage<T> store) : IBlobStoreWriter<T>
    {
        #region IBlobStoreWriter<T>

        /// <inheritdoc/>
        public Task<Stream> OpenWrite(string id, CancellationToken cancellationToken = default)
        {
            return OpenWriteInternal(async ()=> (await store.GetBlobClient(id).ConfigureAwait(false), null), cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public Task<Stream> OpenWrite(IBlobLease lease, CancellationToken cancellationToken = default)
        {
            var blobLease = lease as BlobLease<T>;

            return OpenWriteInternal(()=> Task.FromResult((blobLease!.BlobClient!, (string?)blobLease.LeaseId)), cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public abstract Task Put(string id, Stream contents, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task Put(IBlobLease lease, Stream contents, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public Task Delete(string id, CancellationToken cancellationToken = default)
        {
            return Delete(async ()=> (await store.GetBlobClient(id).ConfigureAwait(false), null), cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public Task Delete(IBlobLease lease, CancellationToken cancellationToken = default)
        {
            var blobLease = lease as BlobLease<T>;

            return Delete(()=> Task.FromResult((blobLease!.BlobClient!, (string?)blobLease.LeaseId)), cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IBlobLease> AcquireLease(string id, bool createIfNotExists = true, CancellationToken cancellationToken = default)
        {
            var  lease = new BlobLease<T>(store, this);
            int retry = 5;

            while(true)
            { 
                try
                { 
                    await lease.Acquire(id, createIfNotExists, cancellationToken);
                    return lease;
                }
                catch(RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
                { 
                    if(retry-- <= 0)
                        throw;

                    await Task.Delay(50);
                }
            }
        }

        #endregion

        #region Protected

        protected internal abstract Task<Stream> OpenWrite(BlobBaseClient client, string? leaseId, CancellationToken cancellationToken);

        #endregion

        #region Private

        private async Task<Stream> OpenWriteInternal(Func<Task<(BlobBaseClient Client, string? LeaseId)>> getBlob, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob = await getBlob();

                return await OpenWrite(blob.Client, blob.LeaseId, cancellationToken).ConfigureAwait(false);
            }
            catch(RequestFailedException ex)
            {
                if(ex.Status == 404)
                    throw new FileNotFoundException("Blob not found", ex);

                throw;
            }
        }

        private async Task Delete(Func<Task<(BlobBaseClient Client, string? LeaseId)>> getBlob, CancellationToken cancellationToken = default)
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
        }

        internal virtual Task CreateIfNotExists(BlobBaseClient blob, string? leaseId, CancellationToken cancellationToken)
        {
            // Default implementation is to do nothing
            return Task.CompletedTask;
        }

        #endregion   
    }

    internal class BlobLease<T>(BaseBlobStorage<T> store, BaseBlobStorageWriter<T> writer) : IBlobLease
    {
        private BlobLeaseClient? _leaseClient;

        public async ValueTask DisposeAsync()
        {
            if(_leaseClient != null)
                await _leaseClient!.ReleaseAsync();
        }

        internal BlobBaseClient? BlobClient { get; private set; }
        internal string          LeaseId    { get; private set; } = "";

        internal async Task Acquire(string id, bool createIfNotExists, CancellationToken cancellationToken)
        {
            LeaseId = Guid.NewGuid().ToString();

            this.BlobClient = await store.GetBlobClient(id, createIfNotExists).ConfigureAwait(true);

            if(createIfNotExists)
                await writer.CreateIfNotExists(this.BlobClient, this.LeaseId, cancellationToken);

            _leaseClient = this.BlobClient.GetBlobLeaseClient(this.LeaseId);

            await _leaseClient.AcquireAsync(TimeSpan.FromSeconds(-1)).ConfigureAwait(true);
        }
    }
}
