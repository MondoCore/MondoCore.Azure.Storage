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
using Azure.Storage.Blobs;
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
    public class AzureAppendBlobStorageWriter<T>(AzureAppendBlobStorage<T> store, AzureStorageRetryPolicy? retryPolicy = null) : BaseBlobStorageWriter<T>(store, retryPolicy)
    {
        /// <inheritdoc/>
        protected internal override Task<Stream> OpenWrite(BlobBaseClient client, string id, string? leaseId, CancellationToken cancellationToken)
        {
            var options = new AppendBlobOpenWriteOptions { };

            if(leaseId != null)
                options.OpenConditions = new AppendBlobRequestConditions { LeaseId = leaseId };

            return (client as AppendBlobClient)!.OpenWriteAsync(true, options, cancellationToken: cancellationToken);
        }

        #region Private

        internal override async Task CreateIfNotExists(BlobBaseClient blob, string? leaseId, CancellationToken cancellationToken)
        {
            if(await blob.ExistsAsync(cancellationToken))
                return;

            var createOptions = new AppendBlobCreateOptions { Conditions = leaseId == null ? null : new AppendBlobRequestConditions { LeaseId = leaseId }};

            await (blob as AppendBlobClient)!.CreateIfNotExistsAsync(createOptions, cancellationToken).ConfigureAwait(false);
        }

        internal override async Task Put(Func<Task<(BlobBaseClient Client, string? LeaseId)>> getBlob, Stream contents, CancellationToken cancellationToken = default)
        {
            try
            { 
                var blob    = await getBlob();
                var options = new AppendBlobAppendBlockOptions { };
                var appendClient = blob.Client as AppendBlobClient;

                // The lease will create
                if(blob.LeaseId == null)
                    await CreateIfNotExists(blob.Client, blob.LeaseId, cancellationToken).ConfigureAwait(false);
                else
                    options.Conditions = new AppendBlobRequestConditions { LeaseId = blob.LeaseId };

                await appendClient!.AppendBlockAsync(contents, options, cancellationToken).ConfigureAwait(false);

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
            catch(RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
            {
                throw new LeaseException(ex);
            }
        }

        #endregion
    }
}
