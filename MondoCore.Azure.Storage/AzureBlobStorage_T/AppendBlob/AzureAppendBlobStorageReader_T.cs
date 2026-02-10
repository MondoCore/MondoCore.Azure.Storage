/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: AzureAppendBlobStorageReader_T.cs			 		    		    
 *        Class(es): AzureAppendBlobStorageReader<T>				           		        
 *          Purpose: Performs read operations on a Azure append blob storage account                           
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

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Performs read operations on a Azure append blob storage account    
    /// </summary>
    public class AzureAppendBlobStorageReader<T>(AzureAppendBlobStorage<T> store) : BaseBlobStorageReader<T>(store)
    {
    }
}
