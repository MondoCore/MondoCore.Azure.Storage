/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: BlobLease.cs			 		    		    
 *        Class(es): BlobLease<T>				           		        
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

using Azure.Storage.Blobs.Specialized;

using MondoCore.Common;

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Lease to lock a blob for writing
    /// </summary>
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
        public   string          BlobId         { get; private set; } = "";

        string IBlobLease.LeaseId => LeaseId;

        internal async Task Acquire(string id, bool createIfNotExists, CancellationToken cancellationToken, bool noAcquire = false)
        {
            LeaseId = Guid.NewGuid().ToString();
            BlobId = id;

            this.BlobClient = await store.GetBlobClient(id, createIfNotExists).ConfigureAwait(true);

            if(!noAcquire)
            { 
                if(createIfNotExists)
                    await writer.CreateIfNotExists(this.BlobClient, this.LeaseId, cancellationToken);

                _leaseClient = this.BlobClient.GetBlobLeaseClient(this.LeaseId);

                await _leaseClient.AcquireAsync(TimeSpan.FromSeconds(-1)).ConfigureAwait(true);
            }
        }

        #region IBlobLease

        public virtual Task Put(Stream content, CancellationToken cancellationToken = default)
        {
            return writer.Put(()=> Task.FromResult((this!.BlobClient!, (string?)this.LeaseId)), content, cancellationToken);
        }

        public virtual Task Delete(CancellationToken cancellationToken = default)
        {
            return writer.Delete(()=> Task.FromResult((this!.BlobClient!, (string?)this.LeaseId)), cancellationToken);
        }

        public virtual Task<Stream> OpenWrite(CancellationToken cancellationToken = default)
        {
            return writer.OpenWriteInternal(()=> Task.FromResult((this!.BlobClient!, (string?)this.LeaseId)), this.BlobId, cancellationToken);
        }

        #endregion
    }
}
