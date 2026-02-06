/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: AzurePageBlobStorageReader_T.cs			 		    		    
 *        Class(es): AzurePageBlobStorageReader<T>			           		        
 *          Purpose: Class to perform read operations on a Azure page blob account                           
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
    /// Class to perform read operations on a Azure page blob account   
    /// </summary>
    public class AzurePageBlobStorageReader<T>(AzurePageBlobStorage<T> store) : BaseBlobStorageReader<T>(store)
    {
    }
}
