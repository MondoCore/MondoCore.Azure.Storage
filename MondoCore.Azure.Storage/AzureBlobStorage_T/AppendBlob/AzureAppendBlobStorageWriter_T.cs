/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: AzureBlobStorageWriter_T.cs			 		    		    
 *        Class(es): AzureBlobStorageWriter<T>				           		        
 *          Purpose: Performs write operations on a Azure append blob storage account                           
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
using Microsoft.Identity.Client;
using MondoCore.Common;

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Performs write operations on a Azure append blob storage account 
    /// </summary>
    public class AzureAppendBlobStorageWriter<T>(AzureAppendBlobStorage<T> store) : BaseBlobStorageWriter<T>(store)
    {
        /// <inheritdoc/>
        public override async Task Put(string id, Stream contents, CancellationToken cancellationToken = default)
        {
            var blob = (await store.GetBlobClient(id).ConfigureAwait(false)) as AppendBlobClient;

            await Put(()=> Task.FromResult((blob!, (string?)null)), contents, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public override Task Put(IBlobLease lease, Stream contents, CancellationToken cancellationToken = default)
        {
            var blobLease = lease as BlobLease<T>;

            return Put(()=> Task.FromResult(((blobLease!.BlobClient! as AppendBlobClient)!, (string?)blobLease.LeaseId)), contents, cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        protected internal override Task<Stream> OpenWrite(BlobBaseClient client, string? leaseId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Cannot open a write stream on this type of Azure Blob Storage. Use AzurePageBlobStorage.");
        }

        #region Private

        internal override async Task CreateIfNotExists(BlobBaseClient blob, string? leaseId, CancellationToken cancellationToken)
        {
            if(await blob.ExistsAsync(cancellationToken))
                return;

            var createOptions = new AppendBlobCreateOptions { Conditions = leaseId == null ? null : new AppendBlobRequestConditions { LeaseId = leaseId }};

            await (blob as AppendBlobClient)!.CreateIfNotExistsAsync(createOptions, cancellationToken).ConfigureAwait(false);
        }

        private async Task Put(Func<Task<(AppendBlobClient Client, string? LeaseId)>> getBlob, Stream contents, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob    = await getBlob();
                var options = new AppendBlobAppendBlockOptions { };

                // The lease will create
                if(blob.LeaseId == null)
                    await CreateIfNotExists(blob.Client, blob.LeaseId, cancellationToken).ConfigureAwait(false);
                else
                    options.Conditions = new AppendBlobRequestConditions { LeaseId = blob.LeaseId };

                await blob.Client!.AppendBlockAsync(contents, options, cancellationToken).ConfigureAwait(false);

                return;
            }
            catch(RequestFailedException ex1) when (ex1.Status == 404)
            { 
                throw new FileNotFoundException("Blob not found", ex1);
            }
            catch(RequestFailedException ex2) when (ex2.Status == 401)
            { 
                throw new UnauthorizedAccessException("Blob not accessible", ex2);
            }
        }

        #endregion
    }
}
