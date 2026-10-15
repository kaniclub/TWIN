using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TrueWindow.Services;

/// <summary>MonoGame/OpenGLのGetData内部一時配列を避け、保持したバッファへ読み戻す。</summary>
internal sealed class ReusableOpenGlReadback : IDisposable
{
    private delegate void GetInteger(int parameter, out int value);
    private Action<int, int>? bindTexture;
    private Action<int, int>? pixelStore;
    private Action<int, int, int, int, IntPtr>? getImage;
    private GetInteger? getInteger;
    private Func<Texture2D, int>? textureId;
    private bool initialized, unavailable;
    private readonly Dictionary<string, (Color[] Pixels, GCHandle Handle)> pinned = new(StringComparer.OrdinalIgnoreCase);
    public string? FailureReason { get; private set; }

    public bool TryRead(string key, Texture2D texture, Color[] pixels)
    {
        if (unavailable) return false;
        try
        {
            if (!initialized) Initialize();
            if (texture.Format != SurfaceFormat.Color || (long)texture.Width * texture.Height > pixels.Length)
                throw new InvalidOperationException("Unexpected texture format or insufficient readback buffer.");
            if (!pinned.TryGetValue(key, out var buffer) || !ReferenceEquals(buffer.Pixels, pixels))
            {
                if (buffer.Handle.IsAllocated) buffer.Handle.Free();
                buffer = (pixels, GCHandle.Alloc(pixels, GCHandleType.Pinned));
                pinned[key] = buffer;
            }
            const int Texture2DTarget = 0x0DE1, TextureBinding2D = 0x8069, PackAlignment = 0x0D05;
            const int Rgba = 0x1908, UnsignedByte = 0x1401;
            getInteger!(TextureBinding2D, out int previousTexture);
            getInteger(PackAlignment, out int previousAlignment);
            try
            {
                bindTexture!(Texture2DTarget, textureId!(texture));
                pixelStore!(PackAlignment, 4);
                getImage!(Texture2DTarget, 0, Rgba, UnsignedByte, buffer.Handle.AddrOfPinnedObject());
            }
            finally
            {
                pixelStore!(PackAlignment, previousAlignment);
                bindTexture!(Texture2DTarget, previousTexture);
            }
            return true;
        }
        catch (Exception ex)
        {
            FailureReason = ex.GetType().Name + ": " + ex.Message;
            unavailable = true;
            Dispose();
            return false;
        }
    }

    private void Initialize()
    {
        if (Marshal.SizeOf<Color>() != 4) throw new NotSupportedException("Color layout is not RGBA32.");
        Assembly assembly = typeof(Texture2D).Assembly;
        Type gl = assembly.GetType("MonoGame.OpenGL.GL", throwOnError: true)!;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Delegate GetBinding(string name) => (Delegate)(gl.GetField(name, Static)?.GetValue(null)
            ?? throw new NotSupportedException("Missing OpenGL entry point: " + name));
        bindTexture = ConvertBinding<Action<int, int>>(GetBinding("BindTexture"));
        pixelStore = ConvertBinding<Action<int, int>>(GetBinding("PixelStore"));
        getImage = ConvertBinding<Action<int, int, int, int, IntPtr>>(GetBinding("GetTexImageInternal"));
        MethodInfo integerMethod = gl.GetMethod("GetInteger", Static, null,
            new[] { typeof(int), typeof(int).MakeByRefType() }, null) ?? throw new NotSupportedException("Missing GetInteger.");
        getInteger = (GetInteger)integerMethod.CreateDelegate(typeof(GetInteger));
        FieldInfo? field = null;
        for (Type? type = typeof(Texture2D); type is not null && field is null; type = type.BaseType)
            field = type.GetField("glTexture", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
        if (field is null || field.FieldType != typeof(int)) throw new NotSupportedException("Missing texture handle.");
        ParameterExpression texture = Expression.Parameter(typeof(Texture2D), "texture");
        textureId = Expression.Lambda<Func<Texture2D, int>>(
            Expression.Field(Expression.Convert(texture, field.DeclaringType!), field), texture).Compile();
        initialized = true;
    }

    private static T ConvertBinding<T>(Delegate original) where T : Delegate
    {
        ParameterInfo[] input = typeof(T).GetMethod("Invoke")!.GetParameters();
        ParameterInfo[] native = original.GetType().GetMethod("Invoke")!.GetParameters();
        if (input.Length != native.Length) throw new NotSupportedException("OpenGL delegate signature changed.");
        ParameterExpression[] parameters = input.Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name)).ToArray();
        Expression[] converted = parameters.Select((parameter, index) => (Expression)Expression.Convert(parameter, native[index].ParameterType)).ToArray();
        return Expression.Lambda<T>(Expression.Invoke(Expression.Constant(original), converted), parameters).Compile();
    }

    public void Dispose()
    {
        foreach (var buffer in pinned.Values) if (buffer.Handle.IsAllocated) buffer.Handle.Free();
        pinned.Clear();
    }
}
