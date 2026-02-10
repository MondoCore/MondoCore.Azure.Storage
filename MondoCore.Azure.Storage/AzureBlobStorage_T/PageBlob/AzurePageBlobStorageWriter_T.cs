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
    public class AzurePageBlobStorageWriter<T>(AzurePageBlobStorage<T> store, AzureStorageRetryPolicy? retryPolicy = null) : BaseBlobStorageWriter<T>(store, retryPolicy)
    {
        internal const int PageSize = 512;
        internal const int MaxWrite = 1024 * 1024 * 4;

        #region Protected

        protected internal override async Task<Stream> OpenWrite(BlobBaseClient client, string id, string? leaseId, CancellationToken cancellationToken)
        {
            // ??? need to make PageBlobWriteStream,Output be a prop so I can reset with a new call to OpenWriteAsync after resizing
            var sizeable = new PageBlobSizeable<T>((client as PageBlobClient)!);
            var storStrm = await ((client as PageBlobClient)!).OpenWriteAsync
            (
                true, 
                0L, 
                new PageBlobOpenWriteOptions 
                { 
                    Size = PageSize, 
                    OpenConditions = leaseId == null ? null : new PageBlobRequestConditions { LeaseId = leaseId } 
                }, 
                cancellationToken
            ).ConfigureAwait(false);

            var stream = new PageBlobWriteStream<T>(storStrm, sizeable!, leaseId);

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

        internal override async Task Put(Func<Task<(BlobBaseClient Client, string? LeaseId)>> getBlob, Stream contents, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob         = await getBlob();
                var len          = contents.Length;
                var adjustedSize = (int)(Math.Ceiling((double)len / (double)PageSize) * PageSize);
                var pageClient   = blob.Client as PageBlobClient;

                using(var strm = await pageClient!.OpenWriteAsync
                                 (
                                     false, 
                                     0L, 
                                     new PageBlobOpenWriteOptions 
                                     { 
                                         Size = adjustedSize, 
                                         OpenConditions = blob.LeaseId == null ? null : new PageBlobRequestConditions { LeaseId = blob.LeaseId }  
                                     }, 
                                     cancellationToken
                                 ).ConfigureAwait(false))
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
            catch(RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
            {
                throw new LeaseException(ex);
            }
        }

        #endregion

        #region ISizeable

        private class PageBlobSizeable<TS> : ISizeable<TS>
        {
            private long _size = PageSize;
            private readonly PageBlobClient _client;

            internal PageBlobSizeable(PageBlobClient client)
            {
                _client = client;
            }

            public long Size => _size;
            internal PageBlobWriteStream<T>? Stream { get; set; }

            public async Task ResizeAsync(long newSize, string? leaseId, CancellationToken cancellationToken)
            {
                var position = this.Stream!.Output!.Position;

                await this.Stream.Output.DisposeAsync();
                var options = new PageBlobRequestConditions { LeaseId = leaseId };

                await _client.ResizeAsync(newSize, options, cancellationToken).ConfigureAwait(false);

                _size = newSize;

                var openWriteOptions = new PageBlobOpenWriteOptions 
                { 
                    Size = newSize, 
                    OpenConditions = leaseId == null ? null : new PageBlobRequestConditions { LeaseId = leaseId } 
                };

                var storStrm = await _client.OpenWriteAsync(false, position, openWriteOptions).ConfigureAwait(false);

                this.Stream.Output = storStrm;

                return;
            }
        }

        #endregion
    }
}
