/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: AzurePageBlobStorageWriter_T.cs			 		    		    
 *        Class(es): AzurePageBlobStorageWriter <T>			           		        
 *          Purpose: Class to perform write operations on a Azure page blob account                           
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
using System.Threading;
using System.Threading.Tasks;

using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using MondoCore.Common;

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Class to perform write operations on a Azure page blob account   
    /// </summary>
    public class AzurePageBlobStorageWriter<T>(AzurePageBlobStorage<T> store) : BaseBlobStorageWriter<T>(store)
    {
        internal const int PageSize = 512;
        internal const int MaxWrite = 1024 * 1024 * 4;

        #region IBlobStoreWriter<T>

        /// <inheritdoc/>
        public override async Task Put(string id, Stream contents, CancellationToken cancellationToken = default)
        {
            var blob = (await store.GetBlobClient(id).ConfigureAwait(false)) as PageBlobClient;

            await Put(()=> Task.FromResult((blob!, (string?)null)), contents, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override Task Put(IBlobLease lease, Stream contents, CancellationToken cancellationToken = default)
        {
            var blobLease = lease as BlobLease<T>;

            return Put(()=> Task.FromResult(((blobLease!.BlobClient! as PageBlobClient)!, (string?)blobLease.LeaseId)), contents, cancellationToken: cancellationToken);
        }
        #endregion

        #region Protected

        protected internal override async Task<Stream> OpenWrite(BlobBaseClient client, string? leaseId, CancellationToken cancellationToken)
        {
            // ??? need to make PageBlobWriteStream,Output be a prop so I can reset with a new call to OpenWriteAsync after resizing
            var sizeable = new PageBlobSizeable((client as PageBlobClient)!);
            var storStrm = await ((client as PageBlobClient)!).OpenWriteAsync(true, 0L, new PageBlobOpenWriteOptions { Size = PageSize }, cancellationToken).ConfigureAwait(false);
            var stream   = new PageBlobWriteStream(storStrm, sizeable);

            sizeable.Stream = stream;

            return stream;
        }

        #endregion

        #region Private

        internal override async Task CreateIfNotExists(BlobBaseClient blob, string? leaseId, CancellationToken cancellationToken = default)
        {
            if(await blob.ExistsAsync(cancellationToken))
                return;

            var createOptions = new PageBlobCreateOptions { Conditions = leaseId == null ? null : new PageBlobRequestConditions { LeaseId = leaseId }};

            await (blob as PageBlobClient)!.CreateIfNotExistsAsync(PageSize, createOptions, cancellationToken).ConfigureAwait(false);
        }

        private async Task Put(Func<Task<(PageBlobClient Client, string? LeaseId)>> getBlob, Stream contents, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob         = await getBlob();
                var len          = contents.Length;
                var adjustedSize = (int)(Math.Ceiling((double)len / (double)PageSize) * PageSize);

                using(var strm = await blob.Client.OpenWriteAsync(false, 0L, new PageBlobOpenWriteOptions { Size = adjustedSize }, cancellationToken).ConfigureAwait(false))
                {
                    var buffer = new byte[MaxWrite];
                    var read   = 0L;
                
                    while(read < len)
                    { 
                        var thisRead = await contents.ReadAsync(buffer, 0, (int)Math.Min(MaxWrite, len - read), cancellationToken).ConfigureAwait(false);

                        if(thisRead == 0)
                            break;

                        var thisWrite = (int)(Math.Ceiling((double)thisRead / (double)PageSize) * PageSize);

                        for(long i = thisRead; i < thisWrite; ++i)
                            buffer[i] = 0;

                        await strm.WriteAsync(buffer, 0, thisWrite, cancellationToken).ConfigureAwait(false);
                        read += thisRead;
                }
            }
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

        #region ISizeable

        private class PageBlobSizeable : ISizeable
        {
            private long _size = PageSize;
            private readonly PageBlobClient _client;

            internal PageBlobSizeable(PageBlobClient client)
            {
                _client = client;
            }

            public long Size => _size;
            internal PageBlobWriteStream? Stream { get; set; }

            // total length  = 18874368
            // last position = 16777216
            // last write    = 2097152
            public async Task ResizeAsync(long newSize)
            {
                var position = this.Stream!.Output.Position;

                this.Stream.Output.Dispose();

                await _client.ResizeAsync(newSize).ConfigureAwait(false);

                _size = newSize;

                var storStrm = await _client.OpenWriteAsync(false, position).ConfigureAwait(false);

                this.Stream.Output = storStrm;

                return;
            }

            public void Resize(long newSize)
            {
                var position = this.Stream!.Output.Position;

               this.Stream.Output.Dispose();

                _client.Resize(newSize);

                _size = newSize;

                var storStrm = _client.OpenWrite(false, position);

                this.Stream.Output = storStrm;

                return;
            }
        }

        #endregion
    }
}
