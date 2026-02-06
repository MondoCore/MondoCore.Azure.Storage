/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: BaseBlobStorageReader_T.cs			 		    		    
 *        Class(es): BaseBlobStorageReader<T>				           		        
 *          Purpose: Base class for Azure blob storage reader                       
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
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Azure;
using Azure.Storage.Blobs.Models;

using MondoCore.Common;

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Base class for Azure blob storage reader
    /// </summary>
    public abstract class BaseBlobStorageReader<T>(BaseBlobStorage<T> store) : IBlobStoreReader<T>
    {
        #region IBlobStoreReader<T>

        /// <inheritdoc/>
        public async Task Get(string id, Stream destination, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob = await store.GetBlobClient(id).ConfigureAwait(false);

                await blob.DownloadToAsync(destination).ConfigureAwait(false);
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

        /// <inheritdoc/>
        public async Task<Stream> OpenRead(string id, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob = await store.GetBlobClient(id).ConfigureAwait(false);

                return await blob.OpenReadAsync(new BlobOpenReadOptions(false)).ConfigureAwait(false);
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

        /// <inheritdoc/>
        public async Task<IEnumerable<string>> Find(string filter, CancellationToken cancellationToken = default)
        {
            var result = new List<string>();

            await this.Enumerate(filter, async (blob)=>
            {
                result.Add(blob.Name);

                await Task.CompletedTask;
            },
            
            false,
            cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task Enumerate(string filter, Func<IBlob, Task> fnEach, bool asynchronous = true, CancellationToken cancellationToken = default)
        {
            var         container = store.ContainerClient;
            var         pages     = container.GetBlobs(cancellationToken: cancellationToken).AsPages();
            List<Task>? tasks     = asynchronous ? new List<Task>() : null;

            foreach(var page in pages)
            {
                var blobs = page.Values;

                foreach(var blob in blobs)
                {
                    if(blob.Name.MatchesWildcard(filter) && (string.IsNullOrWhiteSpace(store.FolderName) || blob.Name.StartsWith(store.FolderName, StringComparison.InvariantCultureIgnoreCase)))
                    { 
                        var ablob = new AzureBlob(blob);

                        ablob.Name = ablob.Name.Substring(store.FolderName!.Length);

                        var task = fnEach(ablob);

                        if(asynchronous)
                            tasks!.Add(task);
                        else
                            await task.ConfigureAwait(false);
                    }
                }
            }

            if(asynchronous)
                await Task.WhenAll(tasks!).ConfigureAwait(false);

            return;
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<IBlob> AsAsyncEnumerable(string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var container = store.ContainerClient;
            var options   = prefix == null ? null : new GetBlobsOptions { Prefix = prefix };
            var pages     = container.GetBlobsAsync(options, cancellationToken).AsPages();

            await foreach(var page in pages)
            {
                var blobs = page.Values;

                foreach(var blob in blobs)
                {
                    if((string.IsNullOrWhiteSpace(store.FolderName) || blob.Name.StartsWith(store.FolderName, StringComparison.InvariantCultureIgnoreCase)))
                    { 
                        var ablob = new AzureBlob(blob);

                        ablob.Name = ablob.Name.Substring(store.FolderName!.Length);

                        yield return ablob;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public async Task<bool> Exists(string id, CancellationToken cancellationToken = default)
        {
            var blob = await store.GetBlobClient(id).ConfigureAwait(false);

            return await blob.ExistsAsync(cancellationToken);
        }

        #endregion
    }

    internal class AzureBlob : IBlob
    { 
        private readonly BlobItem _blob;

        internal AzureBlob(BlobItem blob)
        {
            _blob = blob;
            this.Name = blob.Name;
        }

        public string                       Name        { get; set; }
        public bool                         Deleted     => _blob.Deleted;
        public bool                         Enabled     => true;
        public string                       Version     => "";
        public string                       ContentType => "";
        public DateTimeOffset?              Expires     => null;
        public IDictionary<string, string>? Metadata    => _blob.Metadata;
        public IDictionary<string, string>? Tags        => _blob.Tags;
    }
}
