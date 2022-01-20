namespace UltralightNet.OpenGL;

using System;
using System.IO;
using System.Text;
using System.Numerics;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using UltralightNet;

public unsafe class OpenGLGPUDriver
{
	private readonly GL gl;

	private readonly uint pathProgram;
	private readonly uint fillProgram;

	// TODO: use Lists
	public readonly Dictionary<uint, TextureEntry> textures = new();
	private readonly Dictionary<uint, GeometryEntry> geometries = new();
	private readonly Dictionary<uint, RenderBufferEntry> renderBuffers = new();

	public void Check([CallerLineNumber] int line = default)
	{
#if DEBUG
		var error = gl.GetError();
		if (error is not 0) Console.WriteLine($"{line}: {error}");
#endif
	}

	private readonly bool DSA = true;

	public OpenGLGPUDriver(GL glapi)
	{
		gl = glapi;

		Check();

		#region pathProgram
		{
			uint vert = gl.CreateShader(ShaderType.VertexShader);
			uint frag = gl.CreateShader(ShaderType.FragmentShader);

			gl.ShaderSource(vert, GetShader("shader_fill_path.vert.glsl"));
			gl.ShaderSource(frag, GetShader("shader_fill_path.frag.glsl"));

			gl.CompileShader(vert);
			gl.CompileShader(frag);

			string vertLog = gl.GetShaderInfoLog(vert);
			if (!string.IsNullOrWhiteSpace(vertLog))
			{
				throw new Exception($"Error compiling shader of type, failed with error {vertLog}");
			}
			string fragLog = gl.GetShaderInfoLog(frag);
			if (!string.IsNullOrWhiteSpace(fragLog))
			{
				//throw new Exception($"Error compiling shader of type, failed with error {fragLog}");
			}

			pathProgram = gl.CreateProgram();

			gl.AttachShader(pathProgram, vert);
			gl.AttachShader(pathProgram, frag);

			gl.BindAttribLocation(pathProgram, 0, "in_Position");
			gl.BindAttribLocation(pathProgram, 1, "in_Color");
			gl.BindAttribLocation(pathProgram, 2, "in_TexCoord");

			gl.LinkProgram(pathProgram);
			gl.GetProgram(pathProgram, GLEnum.LinkStatus, out var status);
			if (status == 0)
			{
				throw new Exception($"Program failed to link with error: {gl.GetProgramInfoLog(pathProgram)}");
			}

			gl.DetachShader(pathProgram, vert);
			gl.DetachShader(pathProgram, frag);
			gl.DeleteShader(vert);
			gl.DeleteShader(frag);
		}
		#endregion
		#region fillProgram
		{
			uint vert = gl.CreateShader(ShaderType.VertexShader);
			uint frag = gl.CreateShader(ShaderType.FragmentShader);

			gl.ShaderSource(vert, GetShader("shader_fill.vert.glsl"));
			gl.ShaderSource(frag, GetShader("shader_fill.frag.glsl"));

			gl.CompileShader(vert);
			gl.CompileShader(frag);

			string vertLog = gl.GetShaderInfoLog(vert);
			if (!string.IsNullOrWhiteSpace(vertLog))
			{
				throw new Exception($"Error compiling shader of type, failed with error {vertLog}");
			}
			string fragLog = gl.GetShaderInfoLog(frag);
			if (!string.IsNullOrWhiteSpace(fragLog))
			{
				throw new Exception($"Error compiling shader of type, failed with error {fragLog}");
			}

			fillProgram = gl.CreateProgram();

			gl.AttachShader(fillProgram, vert);
			gl.AttachShader(fillProgram, frag);

			gl.BindAttribLocation(fillProgram, 0, "in_Position");
			gl.BindAttribLocation(fillProgram, 1, "in_Color");
			gl.BindAttribLocation(fillProgram, 2, "in_TexCoord");
			gl.BindAttribLocation(fillProgram, 3, "in_ObjCoord");
			gl.BindAttribLocation(fillProgram, 4, "in_Data0");
			gl.BindAttribLocation(fillProgram, 5, "in_Data1");
			gl.BindAttribLocation(fillProgram, 6, "in_Data2");
			gl.BindAttribLocation(fillProgram, 7, "in_Data3");
			gl.BindAttribLocation(fillProgram, 8, "in_Data4");
			gl.BindAttribLocation(fillProgram, 9, "in_Data5");
			gl.BindAttribLocation(fillProgram, 10, "in_Data6");

			gl.LinkProgram(fillProgram);
			gl.GetProgram(fillProgram, GLEnum.LinkStatus, out var status);
			if (status == 0)
			{
				throw new Exception($"Program failed to link with error: {gl.GetProgramInfoLog(fillProgram)}");
			}

			gl.DetachShader(fillProgram, vert);
			gl.DetachShader(fillProgram, frag);
			gl.DeleteShader(vert);
			gl.DeleteShader(frag);

			gl.UseProgram(fillProgram);
			gl.Uniform1(gl.GetUniformLocation(fillProgram, "Texture1"), 0);
			gl.Uniform1(gl.GetUniformLocation(fillProgram, "Texture2"), 1);
		}
		#endregion

		Check();
	}

	private static string GetShader(string name)
	{
		Assembly assembly = typeof(OpenGLGPUDriver).Assembly;
		Stream stream = assembly.GetManifestResourceStream("UltralightNet.OpenGL.shaders." + name);
		StreamReader resourceStreamReader = new(stream, Encoding.UTF8, false, 16, true);
		return resourceStreamReader.ReadToEnd();
	}


	public ULGPUDriver GetGPUDriver() => new()
	{
		BeginSynchronize = null,
		EndSynchronize = null,
		NextTextureId = () =>
		{
			for (uint i = 1; ; i++)
			{
				if (!textures.ContainsKey(i))
				{
					textures.Add(i, new());
					return i;
				}
			}
		},
		NextGeometryId = () =>
		{
			for (uint i = 1; ; i++)
			{
				if (!geometries.ContainsKey(i))
				{
					geometries.Add(i, new());
					return i;
				}
			}
		},
		NextRenderBufferId = () =>
		{
			for (uint i = 1; ; i++)
			{
				if (!renderBuffers.ContainsKey(i))
				{
					renderBuffers.Add(i, new());
					return i;
				}
			}
		},
		CreateTexture = (entryId, bitmap) =>
		{
			Console.WriteLine("CreateTexture");
			Check();
			var isRT = bitmap.IsEmpty;

			uint textureId;

			uint width = bitmap.Width;
			uint height = bitmap.Height;

			if (DSA)
			{
				gl.CreateTextures(TextureTarget.Texture2D, 1, &textureId);

				if (isRT)
				{
					gl.TextureStorage2D(textureId, 1, SizedInternalFormat.Rgba8, width, height);
				}
				else
				{
					gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
					gl.PixelStore(PixelStoreParameter.UnpackRowLength, (int)(bitmap.RowBytes / bitmap.Bpp));

					void* pixels = (void*)bitmap.LockPixels();

					if (bitmap.Format is ULBitmapFormat.BGRA8_UNORM_SRGB)
					{
						gl.TextureStorage2D(textureId, 1, SizedInternalFormat.Rgba8, width, height);
						gl.TextureSubImage2D(textureId, 0, 0, 0, width, height, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);
					}
					else
					{
						gl.TextureStorage2D(textureId, 1, SizedInternalFormat.R8, width, height);
						gl.TextureSubImage2D(textureId, 0, 0, 0, width, height, PixelFormat.Red, PixelType.UnsignedByte, pixels);
					}

					bitmap.UnlockPixels();
				}

				gl.TextureParameterI(textureId, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
				gl.TextureParameterI(textureId, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

				gl.TextureParameterI(textureId, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
				gl.TextureParameterI(textureId, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
				gl.TextureParameterI(textureId, TextureParameterName.TextureWrapR, (int)GLEnum.Repeat);

				gl.GenerateTextureMipmap(textureId);
			}
			else
			{
				textureId = gl.GenTexture();

				gl.ActiveTexture(TextureUnit.Texture0);
				gl.BindTexture(TextureTarget.Texture2D, textureId);

				gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
				gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
				gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
				gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

				if (isRT)
				{
					Console.WriteLine("RT");
					gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba8, width, height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, null);
				}
				else
				{
					gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
					gl.PixelStore(PixelStoreParameter.UnpackRowLength, (int)(bitmap.RowBytes / bitmap.Bpp));

					void* pixels = (void*)bitmap.LockPixels();

					if (bitmap.Format is ULBitmapFormat.BGRA8_UNORM_SRGB)
					{
						gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba8, width, height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);
					}
					else
					{
						gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.R8, width, height, 0, PixelFormat.Red, PixelType.UnsignedByte, pixels);
					}

					bitmap.UnlockPixels();
				}
				gl.GenerateMipmap(TextureTarget.Texture2D);
			}

			Check();
			textures[entryId].textureId = textureId;
		},
		UpdateTexture = (entryId, bitmap) =>
		{
			Check();
			uint textureId = textures[entryId].textureId;

			if (DSA)
			{
				uint width = bitmap.Width;
				uint height = bitmap.Height;
				gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
				gl.PixelStore(PixelStoreParameter.UnpackRowLength, (int)(bitmap.RowBytes / bitmap.Bpp));

				void* pixels = (void*)bitmap.LockPixels();

				if (bitmap.Format is ULBitmapFormat.BGRA8_UNORM_SRGB)
				{
					gl.TextureSubImage2D(textureId, 0, 0, 0, width, height, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);
				}
				else
				{
					gl.TextureSubImage2D(textureId, 0, 0, 0, width, height, PixelFormat.Red, PixelType.UnsignedByte, pixels);
				}

				bitmap.UnlockPixels();

				gl.GenerateTextureMipmap(textureId);
			}
			else
			{
				gl.ActiveTexture(TextureUnit.Texture0);
				gl.BindTexture(TextureTarget.Texture2D, textureId);

				gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
				gl.PixelStore(PixelStoreParameter.UnpackRowLength, (int)(bitmap.RowBytes / bitmap.Bpp));

				void* pixels = (void*)bitmap.LockPixels();

				if (bitmap.Format is ULBitmapFormat.BGRA8_UNORM_SRGB)
				{
					gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgb8, bitmap.Width, bitmap.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);
				}
				else
				{
					gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.R8, bitmap.Width, bitmap.Height, 0, PixelFormat.Red, PixelType.UnsignedByte, pixels);
				}

				bitmap.UnlockPixels();

				gl.GenerateMipmap(TextureTarget.Texture2D);
			}

			Check();
		},
		DestroyTexture = (id) =>
		{
			var entry = textures[id];

			gl.DeleteTexture(entry.textureId);

			textures.Remove(id);
		},
		CreateRenderBuffer = (id, renderBuffer) =>
		{
			var entry = renderBuffers[id];

			entry.textureEntry = textures[renderBuffer.texture_id];

			uint framebuffer;
			uint textureGLId = entry.textureEntry.textureId;

			if (DSA)
			{
				framebuffer = gl.CreateFramebuffer();
				gl.NamedFramebufferTexture(framebuffer, FramebufferAttachment.ColorAttachment0, textureGLId, 0);
				gl.NamedFramebufferDrawBuffer(framebuffer, ColorBuffer.ColorAttachment0);

#if DEBUG
				var status = gl.CheckNamedFramebufferStatus(framebuffer, FramebufferTarget.Framebuffer);
				if (status is not GLEnum.FramebufferComplete)
				{
					throw new Exception(status.ToString());
				}
#endif
			}
			else
			{
				framebuffer = gl.GenFramebuffer();
				gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
				gl.BindTexture(TextureTarget.Texture2D, textureGLId);
				gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, textureGLId, 0);
				gl.DrawBuffer(DrawBufferMode.ColorAttachment0);

#if DEBUG
				var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
				if (status is not GLEnum.FramebufferComplete)
				{
					throw new Exception(status.ToString());
				}
#endif
				gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			}
			entry.framebuffer = framebuffer;
		},
		DestroyRenderBuffer = (id) =>
		{
			var entry = renderBuffers[id];
			gl.DeleteFramebuffer(entry.framebuffer);
			renderBuffers.Remove(id);
		},
		CreateGeometry = (id, vb, ib) =>
		{
			Check();
			var entry = geometries[id];

			uint vao, vbo, ebo;

			if (DSA)
			{
				vao = gl.CreateVertexArray();
				vbo = gl.CreateBuffer();
				ebo = gl.CreateBuffer();

				gl.NamedBufferData(vbo, vb.size, vb.data, GLEnum.DynamicDraw);
				gl.NamedBufferData(ebo, ib.size, ib.data, GLEnum.DynamicDraw);

				if (vb.Format is ULVertexBufferFormat.VBF_2f_4ub_2f_2f_28f)
				{
					gl.EnableVertexArrayAttrib(vao, 0);
					gl.VertexArrayAttribBinding(vao, 0, 0);
					gl.VertexArrayAttribFormat(vao, 0, 2, GLEnum.Float, false, 0);
					gl.EnableVertexArrayAttrib(vao, 1);
					gl.VertexArrayAttribBinding(vao, 1, 0);
					gl.VertexArrayAttribFormat(vao, 1, 4, GLEnum.UnsignedByte, true, 8);
					gl.EnableVertexArrayAttrib(vao, 2);
					gl.VertexArrayAttribBinding(vao, 2, 0);
					gl.VertexArrayAttribFormat(vao, 2, 2, GLEnum.Float, false, 12);
					gl.EnableVertexArrayAttrib(vao, 3);
					gl.VertexArrayAttribBinding(vao, 3, 0);
					gl.VertexArrayAttribFormat(vao, 3, 2, GLEnum.Float, false, 20);
					gl.EnableVertexArrayAttrib(vao, 4);
					gl.VertexArrayAttribBinding(vao, 4, 0);
					gl.VertexArrayAttribFormat(vao, 4, 4, GLEnum.Float, false, 28);
					gl.EnableVertexArrayAttrib(vao, 5);
					gl.VertexArrayAttribBinding(vao, 5, 0);
					gl.VertexArrayAttribFormat(vao, 5, 4, GLEnum.Float, false, 44);
					gl.EnableVertexArrayAttrib(vao, 6);
					gl.VertexArrayAttribBinding(vao, 6, 0);
					gl.VertexArrayAttribFormat(vao, 6, 4, GLEnum.Float, false, 60);
					gl.EnableVertexArrayAttrib(vao, 7);
					gl.VertexArrayAttribBinding(vao, 7, 0);
					gl.VertexArrayAttribFormat(vao, 7, 4, GLEnum.Float, false, 76);
					gl.EnableVertexArrayAttrib(vao, 8);
					gl.VertexArrayAttribBinding(vao, 8, 0);
					gl.VertexArrayAttribFormat(vao, 8, 4, GLEnum.Float, false, 92);
					gl.EnableVertexArrayAttrib(vao, 9);
					gl.VertexArrayAttribBinding(vao, 9, 0);
					gl.VertexArrayAttribFormat(vao, 9, 4, GLEnum.Float, false, 108);
					gl.EnableVertexArrayAttrib(vao, 10);
					gl.VertexArrayAttribBinding(vao, 10, 0);
					gl.VertexArrayAttribFormat(vao, 10, 4, GLEnum.Float, false, 124);

					gl.VertexArrayVertexBuffer(vao, 0, vbo, 0, 140);
				}
				else
				{
					gl.EnableVertexArrayAttrib(vao, 0);
					gl.VertexArrayAttribBinding(vao, 0, 0);
					gl.VertexArrayAttribFormat(vao, 0, 2, GLEnum.Float, false, 0);
					gl.EnableVertexArrayAttrib(vao, 1);
					gl.VertexArrayAttribBinding(vao, 1, 0);
					gl.VertexArrayAttribFormat(vao, 1, 4, GLEnum.UnsignedByte, true, 8);
					gl.EnableVertexArrayAttrib(vao, 2);
					gl.VertexArrayAttribBinding(vao, 2, 0);
					gl.VertexArrayAttribFormat(vao, 2, 2, GLEnum.Float, false, 12);

					gl.VertexArrayVertexBuffer(vao, 0, vbo, 0, 20);
				}

				gl.VertexArrayElementBuffer(vao, ebo);
			}
			else
			{
				vao = gl.GenVertexArray();

				gl.BindVertexArray(vao);

				vbo = gl.GenBuffer();

				gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
				gl.BufferData(BufferTargetARB.ArrayBuffer, vb.size, vb.data, BufferUsageARB.DynamicDraw);

				if (vb.Format is ULVertexBufferFormat.VBF_2f_4ub_2f_2f_28f)
				{
					const uint stride = 140;

					gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
					gl.VertexAttribPointer(1, 4, VertexAttribPointerType.UnsignedByte, true, stride, (void*)8);
					gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)12);
					gl.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, stride, (void*)20);
					gl.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, stride, (void*)28);
					gl.VertexAttribPointer(5, 4, VertexAttribPointerType.Float, false, stride, (void*)44);
					gl.VertexAttribPointer(6, 4, VertexAttribPointerType.Float, false, stride, (void*)60);
					gl.VertexAttribPointer(7, 4, VertexAttribPointerType.Float, false, stride, (void*)76);
					gl.VertexAttribPointer(8, 4, VertexAttribPointerType.Float, false, stride, (void*)92);
					gl.VertexAttribPointer(9, 4, VertexAttribPointerType.Float, false, stride, (void*)108);
					gl.VertexAttribPointer(10, 4, VertexAttribPointerType.Float, false, stride, (void*)124);

					gl.EnableVertexAttribArray(0);
					gl.EnableVertexAttribArray(1);
					gl.EnableVertexAttribArray(2);
					gl.EnableVertexAttribArray(3);
					gl.EnableVertexAttribArray(4);
					gl.EnableVertexAttribArray(5);
					gl.EnableVertexAttribArray(6);
					gl.EnableVertexAttribArray(7);
					gl.EnableVertexAttribArray(8);
					gl.EnableVertexAttribArray(9);
					gl.EnableVertexAttribArray(10);
				}
				else
				{
					const uint stride = 20;

					gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
					gl.VertexAttribPointer(1, 4, VertexAttribPointerType.UnsignedByte, true, stride, (void*)8);
					gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)12);

					gl.EnableVertexAttribArray(0);
					gl.EnableVertexAttribArray(1);
					gl.EnableVertexAttribArray(2);
				}

				ebo = gl.GenBuffer();

				gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
				gl.BufferData(BufferTargetARB.ElementArrayBuffer, ib.size, ib.data, BufferUsageARB.DynamicDraw);

				Check();

				gl.BindVertexArray(0);
			}

			Check();
			entry.vao = vao;
			entry.vbo = vbo;
			entry.ebo = ebo;
		},
		UpdateGeometry = (id, vb, ib) =>
		{
			var entry = geometries[id];

			if (DSA)
			{
				gl.NamedBufferData(entry.vbo, vb.size, vb.data, GLEnum.DynamicDraw);
				gl.NamedBufferData(entry.ebo, ib.size, ib.data, GLEnum.DynamicDraw);
			}
			else
			{
				gl.BindVertexArray(entry.vao);
				gl.BindBuffer(BufferTargetARB.ArrayBuffer, entry.vbo);
				gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)vb.size, vb.data, BufferUsageARB.DynamicDraw);
				gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, entry.ebo);
				gl.BufferData(BufferTargetARB.ElementArrayBuffer, ib.size, ib.data, BufferUsageARB.DynamicDraw);
				gl.BindVertexArray(0);
			}
			Check();
		},
		DestroyGeometry = (id) =>
		{
			var entry = geometries[id];

			gl.DeleteBuffer(entry.ebo);
			gl.DeleteBuffer(entry.vbo);
			gl.DeleteVertexArray(entry.vao);

			geometries.Remove(id);
		},
		UpdateCommandList = (commandList) =>
		{
			Check();
			var commandSpan = commandList.ToSpan();
			Check();
			gl.Enable(EnableCap.Blend);
			gl.Disable(EnableCap.ScissorTest);
			gl.Disable(EnableCap.DepthTest);
			gl.DepthFunc(DepthFunction.Never);
			gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
			Check();
			foreach (var command in commandSpan)
			{
				Check();
				var gpuState = command.gpu_state;
				var renderBufferEntry = renderBuffers[gpuState.render_buffer_id];

				gl.BindFramebuffer(FramebufferTarget.Framebuffer, renderBufferEntry.framebuffer);
				Check();
				if (command.command_type is ULCommandType.DrawGeometry)
				{
					gl.Viewport(0, 0, gpuState.viewport_width, gpuState.viewport_height);
					Check();
					uint program;

					if (gpuState.shader_type is ULShaderType.FillPath) program = pathProgram; //gl.UseProgram(pathProgram);
					else program = fillProgram; //gl.UseProgram(fillProgram);

					var geometryEntry = geometries[command.geometry_id];
					gl.UseProgram(program);
					Check();
					gl.Uniform4(gl.GetUniformLocation(program, "State"), 0f, (float)gpuState.viewport_width, (float)gpuState.viewport_height, 1f);
					Check();
					Matrix4x4 transform = gpuState.transform.ApplyProjection(gpuState.viewport_width, gpuState.viewport_height, false);
					gl.UniformMatrix4(gl.GetUniformLocation(program, "Transform"), 1, false, &transform.M11);
					Check();
					gl.Uniform4(gl.GetUniformLocation(program, "Scalar4"), 2, &gpuState.scalar_0);
					Check();
					gl.Uniform4(gl.GetUniformLocation(program, "Vector"), 8, &gpuState.vector_0.X);
					Check();
					gl.Uniform1(gl.GetUniformLocation(program, "ClipSize"), (uint)gpuState.clip_size);
					Check();
					gl.UniformMatrix4(gl.GetUniformLocation(program, "Clip"), 8, false, &gpuState.clip_0.M11);
					Check();
					gl.BindVertexArray(geometryEntry.vao);
					Check();

					if (DSA)
					{
						gl.BindTextureUnit(0, textures.ContainsKey(gpuState.texture_1_id) ? textures[gpuState.texture_1_id].textureId : 0);
						gl.BindTextureUnit(1, textures.ContainsKey(gpuState.texture_2_id) ? textures[gpuState.texture_2_id].textureId : 0);
					}
					else
					{
						gl.ActiveTexture(GLEnum.Texture0);
						gl.BindTexture(GLEnum.Texture2D, textures.ContainsKey(gpuState.texture_1_id) ? textures[gpuState.texture_1_id].textureId : 0);
						gl.ActiveTexture(GLEnum.Texture1);
						gl.BindTexture(GLEnum.Texture2D, textures.ContainsKey(gpuState.texture_2_id) ? textures[gpuState.texture_2_id].textureId : 0);
					}
					Check();

					if (gpuState.enable_scissor)
					{
						gl.Enable(EnableCap.ScissorTest);
						gl.Scissor(gpuState.scissor_rect.left, gpuState.scissor_rect.top, (uint)(gpuState.scissor_rect.right - gpuState.scissor_rect.left), (uint)(gpuState.scissor_rect.bottom - gpuState.scissor_rect.top));
					}
					else gl.Disable(EnableCap.ScissorTest);

					Check();

					if (gpuState.enable_blend) gl.Enable(EnableCap.Blend);
					else gl.Disable(EnableCap.Blend);
					Check();
					gl.DrawElements(PrimitiveType.Triangles, command.indices_count, DrawElementsType.UnsignedInt, (void*)(command.indices_offset * sizeof(uint)));
					Check();
				}
				else if (command.command_type is ULCommandType.ClearRenderBuffer)
				{
					Check();
					gl.Disable(GLEnum.ScissorTest);
					gl.ClearColor(0, 0, 0, 0);
					gl.Clear((uint)GLEnum.ColorBufferBit);
					Check();
				}
				else throw new Exception();
			}

			gl.BindVertexArray(0);
			gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			Check();
		}
	};

	// TODO: delete if useless
	public class TextureEntry
	{
		public uint textureId;
	}
	private class GeometryEntry
	{
		public uint vao;
		public uint vbo;
		public uint ebo;
	}
	private class RenderBufferEntry
	{
		public uint framebuffer;
		public TextureEntry textureEntry;
	}
}
