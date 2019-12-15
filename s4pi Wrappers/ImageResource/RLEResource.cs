/*Copyright (c) 2014 Rick (rick 'at' gibbed 'dot' us)

This software is provided 'as-is', without any express or implied
warranty. In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
claim that you wrote the original software. If you use this software
in a product, an acknowledgment in the product documentation would be
appreciated but is not required.

2. Altered source versions must be plainly marked as such, and must not be
misrepresented as being the original software.

3. This notice may not be removed or altered from any source
distribution.*/

/*
 * This wrapper is based on Rick's code and is transformed into s4pi wrapper by Keyi Zhang.
 */
/*
 * This wrapper has been updated to import RLES images by cmarNYC.
 */

using System;
using System.Collections.Generic;
using System.IO;
using s4pi.Interfaces;

namespace s4pi.ImageResource
{
    public class RLEResource : AResource
    {
        const int recommendedApiVersion = 1;
        public override int RecommendedApiVersion { get { return recommendedApiVersion; } }

        static bool checking = s4pi.Settings.Settings.Checking;

        #region Attributes
        private RLEInfo info;
        private MipHeader[] MipHeaders;
        private byte[] data;
        #endregion

        public RLEResource(int APIversion, Stream s) : base(APIversion, s) { if (s == null) { OnResourceChanged(this, EventArgs.Empty); } else { Parse(s); } }

        #region Data I/O
        public void Parse(Stream s)
        {
            if (!ImageResourceSettings.ParseResource) return;
            if (s == null || s.Length == 0) { this.data = new byte[0]; return; }
            BinaryReader r = new BinaryReader(s);
            info = new RLEInfo(s);
            this.MipHeaders = new MipHeader[this.info.mipCount + 1];

            for (int i = 0; i < this.info.mipCount; i++)
            {
                var header = new MipHeader
                {
                    CommandOffset = r.ReadInt32(),
                    Offset2 = r.ReadInt32(),
                    Offset3 = r.ReadInt32(),
                    Offset0 = r.ReadInt32(),
                    Offset1 = r.ReadInt32(),
                };
                if (this.info.Version == RLEVersion.RLES) header.Offset4 = r.ReadInt32();
                MipHeaders[i] = header;
            }

            this.MipHeaders[this.info.mipCount] = new MipHeader
            {
                CommandOffset = MipHeaders[0].Offset2,
                Offset2 = MipHeaders[0].Offset3,
                Offset3 = MipHeaders[0].Offset0,
                Offset0 = MipHeaders[0].Offset1,
            };

            if (this.info.Version == RLEVersion.RLES)
            {
                this.MipHeaders[this.info.mipCount].Offset1 = this.MipHeaders[0].Offset4;
                this.MipHeaders[this.info.mipCount].Offset4 = (int)s.Length;
            }
            else
            {
                this.MipHeaders[this.info.mipCount].Offset1 = (int)s.Length;
            }

            s.Position = 0;
            this.data = r.ReadBytes((int)s.Length);
        }


        protected override Stream UnParse()
        {
            if (this.data == null || this.data.Length == 0) { return new MemoryStream(); }
            else { return new MemoryStream(this.data); }
        }

        public Stream ToDDS()
        {
            if (this.info == null) return null;
            MemoryStream s = new MemoryStream();
            BinaryWriter w = new BinaryWriter(s);
            w.Write(RLEInfo.Signature);
            this.info.UnParse(s);


            // MEED TO BE WRITTEN IN STATIC
            var fullTransparentAlpha = new byte[] { 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var fullTransparentColor = new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var fullOpaqueAlpha = new byte[] { 0x00, 0x05, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

            if (this.info.Version == RLEVersion.RLE2)
            {
                for (int i = 0; i < this.info.mipCount; i++)
                {
                    var mipHeader = this.MipHeaders[i];
                    var nextMipHeader = MipHeaders[i + 1];

                    int blockOffset2, blockOffset3, blockOffset0, blockOffset1;
                    blockOffset2 = mipHeader.Offset2;
                    blockOffset3 = mipHeader.Offset3;
                    blockOffset0 = mipHeader.Offset0;
                    blockOffset1 = mipHeader.Offset1;

                    for (int commandOffset = mipHeader.CommandOffset;
                        commandOffset < nextMipHeader.CommandOffset;
                        commandOffset += 2)
                    {
                        var command = BitConverter.ToUInt16(this.data, commandOffset);

                        var op = command & 3;
                        var count = command >> 2;

                        if (op == 0)
                        {
                            for (int j = 0; j < count; j++)
                            {
                                w.Write(fullTransparentAlpha, 0, 8);
                                w.Write(fullTransparentAlpha, 0, 8);
                            }
                        }
                        else if (op == 1)
                        {
                            for (int j = 0; j < count; j++)
                            {
                                //output.Write(fullOpaqueAlpha, 0, 8);
                                //output.Write(fullTransparentColor, 0, 8);

                                w.Write(this.data, blockOffset0, 2);
                                w.Write(this.data, blockOffset1, 6);
                                w.Write(this.data, blockOffset2, 4);
                                w.Write(this.data, blockOffset3, 4);
                                blockOffset2 += 4;
                                blockOffset3 += 4;
                                blockOffset0 += 2;
                                blockOffset1 += 6;
                            }
                        }
                        else if (op == 2)
                        {
                            for (int j = 0; j < count; j++)
                            {
                                w.Write(fullOpaqueAlpha, 0, 8);
                                w.Write(this.data, blockOffset2, 4);
                                w.Write(this.data, blockOffset3, 4);
                                blockOffset2 += 4;
                                blockOffset3 += 4;
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }

                    if (blockOffset0 != nextMipHeader.Offset0 ||
                        blockOffset1 != nextMipHeader.Offset1 ||
                        blockOffset2 != nextMipHeader.Offset2 ||
                        blockOffset3 != nextMipHeader.Offset3)
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
            else
            {
                for (int i = 0; i < this.info.mipCount; i++)
                {
                    var mipHeader = this.MipHeaders[i];
                    var nextMipHeader = MipHeaders[i + 1];

                    int blockOffset2, blockOffset3, blockOffset0, blockOffset1, blockOffset4;
                    blockOffset2 = mipHeader.Offset2;
                    blockOffset3 = mipHeader.Offset3;
                    blockOffset0 = mipHeader.Offset0;
                    blockOffset1 = mipHeader.Offset1;
                    blockOffset4 = mipHeader.Offset4;
                    var off4 = 0;
                    for (int commandOffset = mipHeader.CommandOffset;
                        commandOffset < nextMipHeader.CommandOffset;
                        commandOffset += 2)
                    {
                        var command = BitConverter.ToUInt16(this.data, commandOffset);

                        var op = command & 3;
                        var count = command >> 2;

                        if (op == 0)
                        {
                            for (int j = 0; j < count; j++)
                            {
                                w.Write(fullTransparentAlpha, 0, 8);
                                w.Write(fullTransparentAlpha, 0, 8);
                            }
                        }
                        else if (op == 1)
                        {
                            for (int j = 0; j < count; j++)
                            {
                                //output.Write(fullOpaqueAlpha, 0, 8);
                                //output.Write(fullTransparentColor, 0, 8);

                                w.Write(this.data, blockOffset0, 2);
                                w.Write(this.data, blockOffset1, 6);
                                blockOffset0 += 2;
                                blockOffset1 += 6;

                                w.Write(this.data, blockOffset2, 4);
                                w.Write(this.data, blockOffset3, 4);
                                blockOffset2 += 4;
                                blockOffset3 += 4;

                                blockOffset4 += 16;
                                off4 += 16;
                            }
                        }
                        else if (op == 2)
                        {
                            for (int j = 0; j < count; j++)
                            {
                                w.Write(this.data, blockOffset0, 2);
                                w.Write(this.data, blockOffset1, 6);
                                w.Write(this.data, blockOffset2, 4);
                                w.Write(this.data, blockOffset3, 4);
                                blockOffset2 += 4;
                                blockOffset3 += 4;
                                blockOffset0 += 2;
                                blockOffset1 += 6;
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }

                    if (blockOffset0 != nextMipHeader.Offset0 ||
                        blockOffset1 != nextMipHeader.Offset1 ||
                        blockOffset2 != nextMipHeader.Offset2 ||
                        blockOffset3 != nextMipHeader.Offset3 ||
                        blockOffset4 != nextMipHeader.Offset4)
                    {
                        throw new InvalidOperationException();
                    }
                }

            }
            s.Position = 0;
            return s;

        }

        public Stream ToBlock4DDS()
        {
            if (this.info == null) return null;
            MemoryStream s = new MemoryStream();
            BinaryWriter w = new BinaryWriter(s);
            w.Write(RLEInfo.Signature);
            this.info.UnParse(s);


            // MEED TO BE WRITTEN IN STATIC
            var fullTransparentAlpha = new byte[] { 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var fullTransparentColor = new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var fullOpaqueAlpha = new byte[] { 0x00, 0x05, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

            if (this.info.Version == RLEVersion.RLE2)
            {
                return null;
            }
            else
            {
                for (int i = 0; i < this.info.mipCount; i++)
                {
                    var mipHeader = this.MipHeaders[i];
                    var nextMipHeader = MipHeaders[i + 1];

                    int blockOffset2, blockOffset3, blockOffset0, blockOffset1, blockOffset4;
                    blockOffset2 = mipHeader.Offset2;
                    blockOffset3 = mipHeader.Offset3;
                    blockOffset0 = mipHeader.Offset0;
                    blockOffset1 = mipHeader.Offset1;
                    blockOffset4 = mipHeader.Offset4;
                    var off4 = 0;
                    for (int commandOffset = mipHeader.CommandOffset;
                        commandOffset < nextMipHeader.CommandOffset;
                        commandOffset += 2)
                    {
                        var command = BitConverter.ToUInt16(this.data, commandOffset);

                        var op = command & 3;
                        var count = command >> 2;

                        if (op == 0)
                        {
                            for (int j = 0; j < count; j++)
                            {
                                w.Write(fullTransparentAlpha, 0, 8);
                                w.Write(fullTransparentAlpha, 0, 8);
                            }
                        }
                        else if (op == 1)
                        {
                            for (int j = 0; j < count; j++)
                            {
                                //output.Write(fullOpaqueAlpha, 0, 8);
                                //output.Write(fullTransparentColor, 0, 8);

                               // w.Write(this.data, blockOffset0, 2);
                               // w.Write(this.data, blockOffset1, 6);
                                blockOffset0 += 2;
                                blockOffset1 += 6;

                               // w.Write(this.data, blockOffset2, 4);
                               // w.Write(this.data, blockOffset3, 4);
                                blockOffset2 += 4;
                                blockOffset3 += 4;

                                w.Write(this.data, blockOffset4, 16);
                                blockOffset4 += 16;
                                off4 += 16;
                            }
                        }
                        else if (op == 2)
                        {
                            for (int j = 0; j < count; j++)
                            {
                                w.Write(fullOpaqueAlpha, 0, 8);
                                w.Write(fullOpaqueAlpha, 0, 8);
                               
                               // w.Write(this.data, blockOffset0, 2);
                               // w.Write(this.data, blockOffset1, 6);
                               // w.Write(this.data, blockOffset2, 4);
                               // w.Write(this.data, blockOffset3, 4);
                                blockOffset2 += 4;
                                blockOffset3 += 4;
                                blockOffset0 += 2;
                                blockOffset1 += 6;
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }

                    if (blockOffset0 != nextMipHeader.Offset0 ||
                        blockOffset1 != nextMipHeader.Offset1 ||
                        blockOffset2 != nextMipHeader.Offset2 ||
                        blockOffset3 != nextMipHeader.Offset3 ||
                        blockOffset4 != nextMipHeader.Offset4)
                    {
                        throw new InvalidOperationException();
                    }
                }

            }
            s.Position = 0;
            return s;

        }

        public void ImportToRLE(Stream input, RLEVersion rleVersion = RLEVersion.RLE2)
        {
            var fullOpaqueAlpha = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

            MemoryStream output = new MemoryStream();
            BinaryReader r = new BinaryReader(input);
            BinaryWriter w = new BinaryWriter(output);

            this.info = new RLEInfo();
            this.info.Parse(input);
            if (this.info.pixelFormat.Fourcc != FourCC.DXT5) throw new InvalidDataException(string.Format("Not a DXT5 format DDS, read {0}", this.info.pixelFormat.Fourcc));

            if (this.info.Depth == 0) this.info.Depth = 1;


            w.Write((uint)FourCC.DXT5);
            if (rleVersion == RLEVersion.RLE2)
            {
                w.Write((uint)0x32454C52);
            }
            else
            {
                w.Write((uint)0x53454C52);
            }
            w.Write((ushort)this.info.Width);
            w.Write((ushort)this.info.Height);
            w.Write((ushort)this.info.mipCount);
            w.Write((ushort)0);

            if (rleVersion == RLEVersion.RLE2)
            {
                var headerOffset = 16;
                var dataOffset = 16 + (20 * this.info.mipCount);
                this.MipHeaders = new MipHeader[this.info.mipCount];

                using (var commandData = new MemoryStream())
                using (var block2Data = new MemoryStream())
                using (var block3Data = new MemoryStream())
                using (var block0Data = new MemoryStream())
                using (var block1Data = new MemoryStream())
                {
                    BinaryWriter commonDataWriter = new BinaryWriter(commandData);
                    for (int mipIndex = 0; mipIndex < this.info.mipCount; mipIndex++)
                    {
                        this.MipHeaders[mipIndex] = new MipHeader()
                        {
                            CommandOffset = (int)commandData.Length,
                            Offset2 = (int)block2Data.Length,
                            Offset3 = (int)block3Data.Length,
                            Offset0 = (int)block0Data.Length,
                            Offset1 = (int)block1Data.Length,
                        };

                        var mipWidth = Math.Max(4, this.info.Width >> mipIndex);
                        var mipHeight = Math.Max(4, this.info.Height >> mipIndex);
                        var mipDepth = Math.Max(1, this.info.Depth >> mipIndex);

                        var mipSize = Math.Max(1, (mipWidth + 3) / 4) * Math.Max(1, (mipHeight + 3) / 4) * 16;
                        var mipData = r.ReadBytes(mipSize);

                        for (int offset = 0; offset < mipSize; )
                        {
                            ushort transparentCount = 0;
                            while (transparentCount < 0x3FFF &&
                                   offset < mipSize &&
                                   TestAlphaAny(mipData, offset, a => a != 0) == false)
                            {
                                transparentCount++;
                                offset += 16;
                            }

                            if (transparentCount > 0)
                            {
                                transparentCount <<= 2;
                                transparentCount |= 0;
                                commonDataWriter.Write(transparentCount);
                                continue;
                            }

                            var opaqueOffset = offset;
                            ushort opaqueCount = 0;
                            while (opaqueCount < 0x3FFF &&
                                   offset < mipSize &&
                                   TestAlphaAll(mipData, offset, a => a == 0xFF) == true)
                            {
                                opaqueCount++;
                                offset += 16;
                            }

                            if (opaqueCount > 0)
                            {
                                for (int i = 0; i < opaqueCount; i++, opaqueOffset += 16)
                                {
                                    block2Data.Write(mipData, opaqueOffset + 8, 4);
                                    block3Data.Write(mipData, opaqueOffset + 12, 4);
                                }

                                opaqueCount <<= 2;
                                opaqueCount |= 2;
                                commonDataWriter.Write(opaqueCount);
                                continue;
                            }

                            var translucentOffset = offset;
                            ushort translucentCount = 0;
                            while (translucentCount < 0x3FFF &&
                                   offset < mipSize &&
                                   TestAlphaAny(mipData, offset, a => a != 0) == true &&
                                   TestAlphaAll(mipData, offset, a => a == 0xFF) == false)
                            {
                                translucentCount++;
                                offset += 16;
                            }

                            if (translucentCount > 0)
                            {
                                for (int i = 0; i < translucentCount; i++, translucentOffset += 16)
                                {
                                    block0Data.Write(mipData, translucentOffset + 0, 2);
                                    block1Data.Write(mipData, translucentOffset + 2, 6);
                                    block2Data.Write(mipData, translucentOffset + 8, 4);
                                    block3Data.Write(mipData, translucentOffset + 12, 4);
                                }

                                translucentCount <<= 2;
                                translucentCount |= 1;
                                commonDataWriter.Write(translucentCount);
                                continue;
                            }

                            throw new NotImplementedException();
                        }
                    }

                    output.Position = dataOffset;

                    commandData.Position = 0;
                    var commandOffset = (int)output.Position;
                    output.Write(commandData.ToArray(), 0, (int)commandData.Length);

                    block2Data.Position = 0;
                    var block2Offset = (int)output.Position;
                    output.Write(block2Data.ToArray(), 0, (int)block2Data.Length);

                    block3Data.Position = 0;
                    var block3Offset = (int)output.Position;
                    output.Write(block3Data.ToArray(), 0, (int)block3Data.Length);

                    block0Data.Position = 0;
                    var block0Offset = (int)output.Position;
                    output.Write(block0Data.ToArray(), 0, (int)block0Data.Length);

                    block1Data.Position = 0;
                    var block1Offset = (int)output.Position;
                    output.Write(block1Data.ToArray(), 0, (int)block1Data.Length);

                    output.Position = headerOffset;
                    for (int i = 0; i < this.info.mipCount; i++)
                    {
                        var mipHeader = this.MipHeaders[i];
                        w.Write(mipHeader.CommandOffset + commandOffset);
                        w.Write(mipHeader.Offset2 + block2Offset);
                        w.Write(mipHeader.Offset3 + block3Offset);
                        w.Write(mipHeader.Offset0 + block0Offset);
                        w.Write(mipHeader.Offset1 + block1Offset);
                    }

                    this.data = output.ToArray();
                }
            }
            else
            {
                var headerOffset = 16;
                var dataOffset = 16 + (24 * this.info.mipCount);
                this.MipHeaders = new MipHeader[this.info.mipCount];

                using (var commandData = new MemoryStream())
                using (var block2Data = new MemoryStream())
                using (var block3Data = new MemoryStream())
                using (var block0Data = new MemoryStream())
                using (var block1Data = new MemoryStream())
                using (var block4Data = new MemoryStream())
                {
                    BinaryWriter commonDataWriter = new BinaryWriter(commandData);
                    for (int mipIndex = 0; mipIndex < this.info.mipCount; mipIndex++)
                    {
                        this.MipHeaders[mipIndex] = new MipHeader()
                        {
                            CommandOffset = (int)commandData.Length,
                            Offset2 = (int)block2Data.Length,
                            Offset3 = (int)block3Data.Length,
                            Offset0 = (int)block0Data.Length,
                            Offset1 = (int)block1Data.Length,
                            Offset4 = (int)block4Data.Length
                        };

                        var mipWidth = Math.Max(4, this.info.Width >> mipIndex);
                        var mipHeight = Math.Max(4, this.info.Height >> mipIndex);
                        var mipDepth = Math.Max(1, this.info.Depth >> mipIndex);

                        var mipSize = Math.Max(1, (mipWidth + 3) / 4) * Math.Max(1, (mipHeight + 3) / 4) * 16;
                        var mipData = r.ReadBytes(mipSize);

                        for (int offset = 0; offset < mipSize; )
                        {
                            ushort transparentCount = 0;
                            while (transparentCount < 0x3FFF &&
                                   offset < mipSize &&
                                   TestAlphaAny(mipData, offset, a => a != 0) == false)
                            {
                                transparentCount++;
                                offset += 16;
                            }

                            if (transparentCount > 0)
                            {
                                transparentCount <<= 2;
                                transparentCount |= 0;
                                commonDataWriter.Write(transparentCount);
                                continue;
                            }

                            var opaqueOffset = offset;
                            ushort opaqueCount = 0;
                            while (opaqueCount < 0x3FFF &&
                                   offset < mipSize &&
                                   TestAlphaAll(mipData, offset, a => a == 0xFF) == true)
                            {
                                opaqueCount++;
                                offset += 16;
                            }

                            if (opaqueCount > 0)
                            {
                                for (int i = 0; i < opaqueCount; i++, opaqueOffset += 16)
                                {
                                    block0Data.Write(mipData, opaqueOffset + 0, 2);
                                    block1Data.Write(mipData, opaqueOffset + 2, 6);
                                    block2Data.Write(mipData, opaqueOffset + 8, 4);
                                    block3Data.Write(mipData, opaqueOffset + 12, 4);
                                   // block4Data.Write(fullOpaqueAlpha, 0, 8);
                                }

                                opaqueCount <<= 2;
                                opaqueCount |= 2;
                                commonDataWriter.Write(opaqueCount);
                                continue;
                            }

                            var translucentOffset = offset;
                            ushort translucentCount = 0;
                            while (translucentCount < 0x3FFF &&
                                   offset < mipSize &&
                                   TestAlphaAny(mipData, offset, a => a != 0) == true &&
                                   TestAlphaAll(mipData, offset, a => a == 0xFF) == false)
                            {
                                translucentCount++;
                                offset += 16;
                            }

                            if (translucentCount > 0)
                            {
                                for (int i = 0; i < translucentCount; i++, translucentOffset += 16)
                                {
                                    block0Data.Write(mipData, translucentOffset + 0, 2);
                                    block1Data.Write(mipData, translucentOffset + 2, 6);
                                    block2Data.Write(mipData, translucentOffset + 8, 4);
                                    block3Data.Write(mipData, translucentOffset + 12, 4);
                                    block4Data.Write(fullOpaqueAlpha, 0, 8);
                                    block4Data.Write(fullOpaqueAlpha, 0, 8);
                                }

                                translucentCount <<= 2;
                                translucentCount |= 1;
                                commonDataWriter.Write(translucentCount);
                                continue;
                            }

                            throw new NotImplementedException();
                        }
                    }

                    output.Position = dataOffset;

                    commandData.Position = 0;
                    var commandOffset = (int)output.Position;
                    output.Write(commandData.ToArray(), 0, (int)commandData.Length);

                    block2Data.Position = 0;
                    var block2Offset = (int)output.Position;
                    output.Write(block2Data.ToArray(), 0, (int)block2Data.Length);

                    block3Data.Position = 0;
                    var block3Offset = (int)output.Position;
                    output.Write(block3Data.ToArray(), 0, (int)block3Data.Length);

                    block0Data.Position = 0;
                    var block0Offset = (int)output.Position;
                    output.Write(block0Data.ToArray(), 0, (int)block0Data.Length);

                    block1Data.Position = 0;
                    var block1Offset = (int)output.Position;
                    output.Write(block1Data.ToArray(), 0, (int)block1Data.Length);

                    block4Data.Position = 0;
                    var block4Offset = (int)output.Position;
                    output.Write(block4Data.ToArray(), 0, (int)block4Data.Length);

                    output.Position = headerOffset;
                    for (int i = 0; i < this.info.mipCount; i++)
                    {
                        var mipHeader = this.MipHeaders[i];
                        w.Write(mipHeader.CommandOffset + commandOffset);
                        w.Write(mipHeader.Offset2 + block2Offset);
                        w.Write(mipHeader.Offset3 + block3Offset);
                        w.Write(mipHeader.Offset0 + block0Offset);
                        w.Write(mipHeader.Offset1 + block1Offset);
                        w.Write(mipHeader.Offset4 + block4Offset);
                    }

                    this.data = output.ToArray();
                }
            }
        }

        private static bool TrueForAny<T>(T[] array, int offset, int count, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (match == null)
            {
                throw new ArgumentNullException("match");
            }

            if (offset < 0 || offset > array.Length)
            {
                throw new IndexOutOfRangeException();
            }

            var end = offset + count;
            if (end < 0 || end > array.Length)
            {
                throw new IndexOutOfRangeException();
            }

            for (int index = offset; index < end; index++)
            {
                if (match(array[index]) == true)
                {
                    return true;
                }
            }
            return false;
        }

        private static unsafe void UnpackAlpha(byte[] array, int offset, byte* alpha, out ulong bits)
        {
            alpha[0] = array[offset + 0];
            alpha[1] = array[offset + 1];

            if (alpha[0] > alpha[1])
            {
                alpha[2] = (byte)((6 * alpha[0] + 1 * alpha[1] + 3) / 7);
                alpha[3] = (byte)((5 * alpha[0] + 2 * alpha[1] + 3) / 7);
                alpha[4] = (byte)((4 * alpha[0] + 3 * alpha[1] + 3) / 7);
                alpha[5] = (byte)((3 * alpha[0] + 4 * alpha[1] + 3) / 7);
                alpha[6] = (byte)((2 * alpha[0] + 5 * alpha[1] + 3) / 7);
                alpha[7] = (byte)((1 * alpha[0] + 6 * alpha[1] + 3) / 7);
            }
            else
            {
                alpha[2] = (byte)((4 * alpha[0] + 1 * alpha[1] + 2) / 5);
                alpha[3] = (byte)((3 * alpha[0] + 2 * alpha[1] + 2) / 5);
                alpha[4] = (byte)((2 * alpha[0] + 3 * alpha[1] + 2) / 5);
                alpha[5] = (byte)((1 * alpha[0] + 4 * alpha[1] + 2) / 5);
                alpha[6] = 0x00;
                alpha[7] = 0xFF;
            }

            bits = 0;
            for (int i = 7; i >= 2; i--)
            {
                bits <<= 8;
                bits |= array[offset + i];
            }
        }

        private static unsafe bool TestAlphaAll(byte[] array, int offset, Func<byte, bool> test)
        {
            var alpha = stackalloc byte[16];
            ulong bits;

            UnpackAlpha(array, offset, alpha, out bits);

            for (int i = 0; i < 16; i++)
            {
                if (test(alpha[bits & 7]) == false)
                {
                    return false;
                }

                bits >>= 3;
            }

            return true;
        }

        private static unsafe bool TestAlphaAny(byte[] array, int offset, Func<byte, bool> test)
        {
            var alpha = stackalloc byte[16];
            ulong bits;

            UnpackAlpha(array, offset, alpha, out bits);

            for (int i = 0; i < 16; i++)
            {
                if (test(alpha[bits & 7]) == true)
                {
                    return true;
                }

                bits >>= 3;
            }

            return false;
        }

        #endregion

        #region Content Fields
        public byte[] RawData { get { return this.data; } }
        #endregion

        #region Sub-Types

        public class MipHeader
        {
            public int CommandOffset { get; internal set; }
            public int Offset0 { get; internal set; }
            public int Offset1 { get; internal set; }
            public int Offset2 { get; internal set; }
            public int Offset3 { get; internal set; }
            public int Offset4 { get; internal set; }
        }

        public class RLEInfo
        {
            public const uint Signature = 0x20534444;
            public uint size { get { return (18 * 4) + PixelFormat.StructureSize + (5 * 4); } }
            public HeaderFlags headerFlags { get; internal set; }
            public int Height { get; internal set; }
            public int Width { get; internal set; }
            public uint PitchOrLinearSize { get; internal set; }
            public int Depth = 1;
            //public uint mipMapCount { get; internal set; }
            private byte[] Reserved1 = new byte[11 * 4];
            public PixelFormat pixelFormat { get; internal set; }
            public uint surfaceFlags { get; internal set; }
            public uint cubemapFlags { get; internal set; }
            public byte[] reserved2 = new byte[3 * 4];
            public RLEVersion Version { get; internal set; }
            public RLEInfo() { this.pixelFormat = new PixelFormat(); }
            public bool HasSpecular { get { return this.Version == RLEVersion.RLES; } }
            public uint mipCount { get; internal set; }
            public ushort Unknown0E { get; internal set; }

            public RLEInfo(Stream s, bool check = true)
                : this(s, FourCC.DXT5, check) { }

            public RLEInfo(Stream s, FourCC fourCC, bool check)
            {
                s.Position = 0;
                BinaryReader r = new BinaryReader(s);
                uint fourcc = r.ReadUInt32();
                if (check) if (fourcc != (uint)fourCC) throw new NotImplementedException(string.Format("Expected format: 0x{0:X8}, read 0x{1:X8}", (uint)fourCC, fourcc));
                this.Version = (RLEVersion)r.ReadUInt32();
                this.Width = r.ReadUInt16();
                this.Height = r.ReadUInt16();
                this.mipCount = r.ReadUInt16();
                this.Unknown0E = r.ReadUInt16();
                this.headerFlags = HeaderFlags.Texture;
                if (this.Unknown0E != 0) throw new InvalidDataException(string.Format("Expected 0, read 0x{0:X8}", this.Unknown0E));
                this.pixelFormat = new PixelFormat();
            }


            public void Parse(Stream s)
            {
                BinaryReader r = new BinaryReader(s);
                uint signature = r.ReadUInt32();
                if (signature != Signature) throw new InvalidDataException(string.Format("Expected signature 0x{0:X8}, read 0x{1:X8}", Signature, signature));
                uint size = r.ReadUInt32();
                if (size != this.size) throw new InvalidDataException(string.Format("Expected size: 0x{0:X8}, read 0x{1:X8}", this.size, size));
                this.headerFlags = (HeaderFlags)r.ReadUInt32();
                if ((this.headerFlags & HeaderFlags.Texture) != HeaderFlags.Texture) throw new InvalidDataException(string.Format("Expected 0x{0:X8}, read 0x{1:X8}", (uint)HeaderFlags.Texture, (uint)this.headerFlags));
                this.Height = r.ReadInt32();
                this.Width = r.ReadInt32();
                if (this.Height > ushort.MaxValue || this.Width > ushort.MaxValue) throw new InvalidDataException("Invalid width or length");
                this.PitchOrLinearSize = r.ReadUInt32();
                this.Depth = r.ReadInt32();
                if (this.Depth != 0 && this.Depth != 1) throw new InvalidDataException(string.Format("Expected depth 1 or 0, read 0x{0:X8}", this.Depth));
                this.mipCount = r.ReadUInt32();
                if (this.mipCount > 16) throw new InvalidDataException(string.Format("Expected mini map count less than 16, read 0x{0:X8}", this.mipCount));
                r.ReadBytes(this.Reserved1.Length);
                this.pixelFormat = new PixelFormat();
                this.pixelFormat.Parse(s);
                this.surfaceFlags = r.ReadUInt32();
                this.cubemapFlags = r.ReadUInt32();
                r.ReadBytes(this.reserved2.Length);
            }

            public void UnParse(Stream s)
            {
                BinaryWriter w = new BinaryWriter(s);
                w.Write(this.size);
                w.Write((uint)this.headerFlags);
                w.Write(this.Height);
                w.Write(this.Width);
                w.Write(this.PitchOrLinearSize);
                w.Write(this.Depth);
                w.Write((uint)this.mipCount);
                w.Write(this.Reserved1);
                this.pixelFormat.UnParse(s);
                w.Write(this.surfaceFlags);
                w.Write(this.cubemapFlags);
                w.Write(this.reserved2);
            }
        }

        


        public enum RLEVersion : uint
        {
            RLE2 = 0x32454C52,
            RLES = 0x53454C52
        }

        public enum HeaderFlags : uint
        {
            Texture = 0x00001007, // DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT 
            Mipmap = 0x00020000, // DDSD_MIPMAPCOUNT
            Volume = 0x00800000, // DDSD_DEPTH
            Pitch = 0x00000008, // DDSD_PITCH
            LinerSize = 0x00080000, // DDSD_LINEARSIZE
        }
        
        #endregion
    }

    public class RLEResourceTS4Handler : AResourceHandler
    {
        public RLEResourceTS4Handler()
        {
            this.Add(typeof(RLEResource), new List<string>(new string[] { "0x3453CF95","0xBA856C78", }));
        }
    }
}
