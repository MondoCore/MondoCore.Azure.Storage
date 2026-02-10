/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: AzureBlobStorageReader_T.cs			 		    		    
 *        Class(es): AzureBlobStorageReader<T>				           		        
 *          Purpose: Class to perform read operations on a Azure storage account                           
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

namespace MondoCore.Azure.Storage
{
    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Class to perform read operations on a Azure storage account  
    /// </summary>
    public class AzureBlobStorageReader<T>(AzureBlobStorage<T> store) : BaseBlobStorageReader<T>(store)
    {
    }
}
