/***************************************************************************
 *                                                                          
 *    The MondoCore Libraries  							                    
 *                                                                          
 *        Namespace: MondoCore.Azure.Storage				            
 *             File: PageBlobWriteStream_T.cs			 		    		    
 *        Class(es): PageBlobWriteStream<T>				           		        
 *          Purpose: Stream for writing to page blobs                       
 *                                                                          
 *  Original Author: Jim Lightfoot                                          
 *    Creation Date: 7 Feb 2026                                             
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
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MondoCore.Azure.Storage.FunctionalTests")]

namespace MondoCore.Azure.Storage
{
    internal interface ISizeable<TS>
    {
        long Size { get; }
        Task ResizeAsync(long newSize, string? leaseId, CancellationToken cancellationToken);
    }

    /****************************************************************************/
    /****************************************************************************/
    /// <summary>
    /// Writes to a secondary stream in chunks
    /// </summary>
    internal class PageBlobWriteStream<T> : Stream
    {
        private readonly ISizeable<T> _sizeable;
        private long                   _length   = 0L;
        private long                   _position = 0L;
        private readonly byte[]        _buffer;
        private readonly int           _bufferSize;
        private int                    _bufferPosition = 0;
        private string?                _leaseId;

        /****************************************************************************/
        internal PageBlobWriteStream(Stream output, ISizeable<T> sizeable, string? leaseId, int bufferSize = 4096 * 1024)
        {
            this.Output = output;
            _sizeable   = sizeable;
            _bufferSize = Math.Max(AzurePageBlobStorageWriter<T>.PageSize, (int)(Math.Floor((double)bufferSize / AzurePageBlobStorageWriter<T>.PageSize) * AzurePageBlobStorageWriter<T>.PageSize));
            _buffer     = new byte[_bufferSize];
            _leaseId    = leaseId;
        }

        public override long Position   { get => _position; set => throw new NotSupportedException(); }
        public override long Length     => _length;
        public override bool CanWrite   => true;
        public override bool CanSeek    => false;
        public override bool CanRead    => false;
        internal Stream?     Output     { get; set; }

        #region Write Methods

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if(buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if(offset >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if((offset + count) >= buffer.Length)
                count = buffer.Length - offset;
                
            if(count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if(_bufferPosition + count <= _bufferSize)
            {
                Buffer.BlockCopy(buffer, offset, _buffer, _bufferPosition, count);
                _bufferPosition += count;
                _position += count;
                _length += count;

                await CheckFlushBufferAsync(cancellationToken).ConfigureAwait(false);

                return;
            }

            var written = 0;

            while(written < count)
            {
                var thisCopy = Math.Min(count - written, _bufferSize);

                Buffer.BlockCopy(buffer, offset + written, _buffer, _bufferPosition, thisCopy);
                _bufferPosition += thisCopy;
                _position += thisCopy;
                _length += thisCopy;
                written += thisCopy;

                await CheckFlushBufferAsync(cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if(_bufferPosition > 0)
            {
                var bytesToWrite = AdjustedSize(_bufferPosition);

                if(_bufferPosition < bytesToWrite)
                {
                    for(var i = _bufferPosition; i < bytesToWrite; ++i)
                        _buffer[i] = 0;
                }

                if(_sizeable.Size < _length)
                    await _sizeable.ResizeAsync(AdjustedSize(_length), _leaseId, cancellationToken);

                await this.Output!.WriteAsync(_buffer, 0, (int)bytesToWrite, cancellationToken);

                _bufferPosition = 0;
            }
        }

        private long AdjustedSize(long size)
        {
            return (long)(Math.Ceiling((double)size / AzurePageBlobStorageWriter<T>.PageSize)) * AzurePageBlobStorageWriter<T>.PageSize;
        }

        public override async ValueTask DisposeAsync()
        {
            await FlushAsync();

            await this.Output!.DisposeAsync();

            this.Output = null;
  
            await base.DisposeAsync();
        }

        #endregion

        #region Not Supported

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion

        #region Private

        private async Task CheckFlushBufferAsync(CancellationToken cancellationToken)
        {
            if(_bufferPosition >= _bufferSize)
            {
                var pos = this.Output!.Position;

                if(_sizeable.Size < _length)
                    await _sizeable.ResizeAsync(AdjustedSize(_length), _leaseId, cancellationToken).ConfigureAwait(false);

                 var pos2 = this.Output.Position;

                 await this.Output.WriteAsync(_buffer, 0, _bufferSize, cancellationToken).ConfigureAwait(false);

                _bufferPosition = 0;
            }
        }

        #endregion
    }
}
