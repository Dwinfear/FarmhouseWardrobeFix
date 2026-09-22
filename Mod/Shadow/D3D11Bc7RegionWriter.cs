using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace FarmhouseWardrobeFix
{
    internal static class D3D11Bc7RegionWriter
    {
        private const uint DxgiFormatBc7Typeless = 97;
        private const uint DxgiFormatBc7Unorm = 98;
        private const uint DxgiFormatBc7UnormSrgb = 99;
        private const uint D3D11UsageDefault = 0;

        private static readonly Guid IidD3D11Texture2D =
            new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        internal static int WritePatched(
            Texture2D target,
            IReadOnlyList<NativeBc7Region> regions)
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
                throw new PlatformNotSupportedException(
                    $"Direct3D11 is required; current API is {SystemInfo.graphicsDeviceType}.");
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            IntPtr resource = target.GetNativeTexturePtr();
            if (resource == IntPtr.Zero)
                throw new InvalidOperationException("GetNativeTexturePtr returned NULL.");

            IntPtr texture2D = IntPtr.Zero;
            Guid texture2DIid = IidD3D11Texture2D;
            int queryHr = Method<QueryInterfaceDelegate>(resource, 0)(
                resource,
                ref texture2DIid,
                out texture2D);
            ThrowIfFailed(queryHr, "ID3D11Resource::QueryInterface(ID3D11Texture2D)");
            if (texture2D == IntPtr.Zero)
                throw new InvalidOperationException(
                    "QueryInterface returned a NULL ID3D11Texture2D.");

            IntPtr device = IntPtr.Zero;
            IntPtr context = IntPtr.Zero;
            try
            {
                Method<GetDescDelegate>(texture2D, 10)(texture2D, out D3D11Texture2DDesc desc);
                int residentMipOffset = ValidateAndGetResidentMipOffset(desc, target);

                Method<GetDeviceDelegate>(texture2D, 3)(texture2D, out device);
                if (device == IntPtr.Zero)
                    throw new InvalidOperationException("ID3D11Resource::GetDevice returned NULL.");

                Method<GetImmediateContextDelegate>(device, 40)(device, out context);
                if (context == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "ID3D11Device::GetImmediateContext returned NULL.");

                UpdateSubresourceDelegate update =
                    Method<UpdateSubresourceDelegate>(context, 48);
                int updated = 0;

                foreach (NativeBc7Region region in regions)
                {
                    int nativeMip = region.Mip - residentMipOffset;
                    if (nativeMip < 0 || nativeMip >= (int)desc.MipLevels)
                        continue;

                    int mipWidth = Math.Max(1, target.width >> region.Mip);
                    int mipHeight = Math.Max(1, target.height >> region.Mip);
                    uint rowPitch = checked((uint)(region.BlockWidth * 16));
                    var box = new D3D11Box
                    {
                        Left = checked((uint)(region.BlockX * 4)),
                        Top = checked((uint)(region.BlockY * 4)),
                        Front = 0,
                        Right = checked((uint)Math.Min(
                            mipWidth,
                            (region.BlockX + region.BlockWidth) * 4)),
                        Bottom = checked((uint)Math.Min(
                            mipHeight,
                            (region.BlockY + region.BlockHeight) * 4)),
                        Back = 1
                    };

                    GCHandle pinned = default;
                    try
                    {
                        pinned = GCHandle.Alloc(region.PatchedBytes, GCHandleType.Pinned);
                        update(
                            context,
                            texture2D,
                            checked((uint)nativeMip),
                            ref box,
                            pinned.AddrOfPinnedObject(),
                            rowPitch,
                            0);
                    }
                    finally
                    {
                        if (pinned.IsAllocated)
                            pinned.Free();
                    }
                    updated++;
                }

                if (updated == 0)
                    throw new InvalidOperationException(
                        $"No patch regions are resident in native texture " +
                        $"{desc.Width}x{desc.Height}.");

                return checked((int)desc.Width);
            }
            finally
            {
                Release(context);
                Release(device);
                Release(texture2D);
            }
        }

        private static int ValidateAndGetResidentMipOffset(
            D3D11Texture2DDesc desc,
            Texture2D target)
        {
            if (desc.Format != DxgiFormatBc7Typeless &&
                desc.Format != DxgiFormatBc7Unorm &&
                desc.Format != DxgiFormatBc7UnormSrgb)
                throw new InvalidOperationException(
                    $"Native D3D11 format is {desc.Format}; expected BC7 (97/98/99).");

            if (desc.ArraySize != 1 || desc.SampleDesc.Count != 1 || desc.MipLevels == 0)
                throw new InvalidOperationException(
                    $"Unexpected native layout: {desc.Width}x{desc.Height}, " +
                    $"mips={desc.MipLevels}, array={desc.ArraySize}, " +
                    $"samples={desc.SampleDesc.Count}.");

            if (desc.Usage != D3D11UsageDefault)
                throw new InvalidOperationException(
                    $"Native lightmap uses D3D11 usage {desc.Usage}; expected DEFAULT (0).");

            int width = target.width;
            int height = target.height;
            int offset = 0;
            while ((width > (int)desc.Width || height > (int)desc.Height) &&
                   width > 1 && height > 1)
            {
                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);
                offset++;
            }

            if (width != (int)desc.Width || height != (int)desc.Height)
                throw new InvalidOperationException(
                    $"Native streaming size {desc.Width}x{desc.Height} is not a mip of " +
                    $"logical size {target.width}x{target.height}.");

            return offset;
        }

        private static T Method<T>(IntPtr instance, int index) where T : Delegate
        {
            IntPtr vtable = Marshal.ReadIntPtr(instance);
            IntPtr function = Marshal.ReadIntPtr(vtable, checked(index * IntPtr.Size));
            if (function == IntPtr.Zero)
                throw new InvalidOperationException($"COM vtable entry {index} is NULL.");
            return Marshal.GetDelegateForFunctionPointer<T>(function);
        }

        private static void Release(IntPtr instance)
        {
            if (instance != IntPtr.Zero)
                Method<ReleaseDelegate>(instance, 2)(instance);
        }

        private static void ThrowIfFailed(int hresult, string operation)
        {
            if (hresult < 0)
                throw new COMException(
                    $"{operation} failed with HRESULT 0x{hresult:X8}.",
                    hresult);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DxgiSampleDesc
        {
            public uint Count;
            public uint Quality;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3D11Texture2DDesc
        {
            public uint Width;
            public uint Height;
            public uint MipLevels;
            public uint ArraySize;
            public uint Format;
            public DxgiSampleDesc SampleDesc;
            public uint Usage;
            public uint BindFlags;
            public uint CpuAccessFlags;
            public uint MiscFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3D11Box
        {
            public uint Left;
            public uint Top;
            public uint Front;
            public uint Right;
            public uint Bottom;
            public uint Back;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int QueryInterfaceDelegate(
            IntPtr self,
            ref Guid interfaceId,
            out IntPtr result);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GetDeviceDelegate(IntPtr self, out IntPtr device);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GetDescDelegate(
            IntPtr self,
            out D3D11Texture2DDesc desc);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GetImmediateContextDelegate(
            IntPtr self,
            out IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void UpdateSubresourceDelegate(
            IntPtr self,
            IntPtr destinationResource,
            uint destinationSubresource,
            ref D3D11Box destinationBox,
            IntPtr sourceData,
            uint sourceRowPitch,
            uint sourceDepthPitch);
    }
}
