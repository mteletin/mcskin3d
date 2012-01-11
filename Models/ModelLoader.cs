﻿//
//    MCSkin3D, a 3d skin management studio for Minecraft
//    Copyright (C) 2011-2012 Altered Softworks & MCSkin3D Team
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Paril.OpenGL;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.Windows.Forms;
using System.IO;

namespace MCSkin3D
{
	public static class ModelLoader
	{
		// 3 = front-top-left
		// 2 = front-top-right
		// 1 = front-bottom-right
		// 0 = front-bottom-left
		// 7 = back-top-left
		// 6 = back-top-right
		// 5 = back-bottom-right
		// 4 = back-bottom-left
		static Vector3[] CreateBox(float width, float height, float length)
		{
			Vector3[] vertices = new Vector3[8];

			width /= 2;
			height /= 2;
			length /= 2;

			vertices[0] = new Vector3(-width, -height, length);
			vertices[1] = new Vector3(width, -height, length);
			vertices[2] = new Vector3(width, height, length);
			vertices[3] = new Vector3(-width, height, length);

			vertices[4] = new Vector3(-width, -height, -length);
			vertices[5] = new Vector3(width, -height, -length);
			vertices[6] = new Vector3(width, height, -length);
			vertices[7] = new Vector3(-width, height, -length);

			return vertices;
		}

		static Vector3[] CreateBox(float size)
		{
			return CreateBox(size, size, size);
		}

		enum FaceLocation
		{
			Front,
			Back,
			Top,
			Bottom,
			Left,
			Right
		}

		static Vector3[] GetFace(FaceLocation location, Vector3[] box)
		{
			switch (location)
			{
			case FaceLocation.Front:
				return new Vector3[] { box[3], box[2], box[1], box[0] };
			case FaceLocation.Top:
				return new Vector3[] { box[7], box[6], box[2], box[3] };
			case FaceLocation.Bottom:
				return new Vector3[] { box[5], box[4], box[0], box[1] };
			case FaceLocation.Back:
				return new Vector3[] { box[4], box[5], box[6], box[7] };
			case FaceLocation.Left:
				return new Vector3[] { box[7], box[3], box[0], box[4] };
			case FaceLocation.Right:
				return new Vector3[] { box[2], box[6], box[5], box[1] };
			}

			return null;
		}

		static Vector2[] TexCoordBox(int x, int y, int w, int h)
		{
			const float sw = 64.0f;
			const float sh = 32.0f;

			float rx = x / sw;
			float ry = y / sh;
			float rw = w / sw;
			float rh = h / sh;

			return new Vector2[]
			{
				new Vector2(rx, ry),
				new Vector2(rx + rw, ry),
				new Vector2(rx + rw, ry + rh),
				new Vector2(rx, ry + rh),
			};
		}

		static Vector2[] TexCoordBoxPrecise(int x, int y, int w, int h,
			int i1, int i2, int i3, int i4)
		{
			var box = TexCoordBox(x, y, w, h);
			return new Vector2[] { box[i1], box[i2], box[i3], box[i4] };
		}

		static Vector2[] InvertCoords(Vector2[] coords)
		{
			return new Vector2[] { coords[3], coords[2], coords[1], coords[0] };
		}

		public static Dictionary<string, Model> Models = new Dictionary<string, Model>();

		public static void InvertBottomFaces()
		{
			foreach (var m in Models.Values)
				foreach (var mesh in m.Meshes)
					foreach (var face in mesh.Faces)
					{
						if (face.Downface)
						{
							float minY = 1, maxY = 0;

							for (int i = 0; i < 4; ++i)
							{
								if (face.TexCoords[i].Y < minY)
									minY = face.TexCoords[i].Y;
								if (face.TexCoords[i].Y > maxY)
									maxY = face.TexCoords[i].Y;
							}

							for (int i = 0; i < 4; ++i)
							{
								if (face.TexCoords[i].Y == minY)
									face.TexCoords[i].Y = maxY;
								else
									face.TexCoords[i].Y = minY;
							}
						}
					}
		}

		/*
		 * Magic from Java
		 */
		public class PositionTextureVertex
		{
			public Vector3 vector3D;
			public float texturePositionX;
			public float texturePositionY;

			public PositionTextureVertex(float f, float f1, float f2, float f3, float f4) :
				this(new Vector3(f, f1, f2), f3, f4)
			{
			}

			public PositionTextureVertex setTexturePosition(float f, float f1)
			{
				return new PositionTextureVertex(this, f, f1);
			}

			public PositionTextureVertex(PositionTextureVertex positiontexturevertex, float f, float f1)
			{
				vector3D = positiontexturevertex.vector3D;
				texturePositionX = f;
				texturePositionY = f1;
			}

			public PositionTextureVertex(Vector3 vec3d, float f, float f1)
			{
				vector3D = vec3d;
				texturePositionX = f;
				texturePositionY = f1;
			}
		}

		public class TexturedQuad
		{
			public PositionTextureVertex[] vertexPositions;
			public int nVertices;

			public TexturedQuad(PositionTextureVertex[] apositiontexturevertex)
			{
				nVertices = 0;
				vertexPositions = apositiontexturevertex;
				nVertices = apositiontexturevertex.Length;
			}

			public TexturedQuad(PositionTextureVertex[] apositiontexturevertex, int i, int j, int k, int l, float f, float f1) :
				this(apositiontexturevertex)
			{
				float f2 = 0.0F / f;
				float f3 = 0.0F / f1;
				apositiontexturevertex[0] = apositiontexturevertex[0].setTexturePosition((float)k / f - f2, (float)j / f1 + f3);
				apositiontexturevertex[1] = apositiontexturevertex[1].setTexturePosition((float)i / f + f2, (float)j / f1 + f3);
				apositiontexturevertex[2] = apositiontexturevertex[2].setTexturePosition((float)i / f + f2, (float)l / f1 - f3);
				apositiontexturevertex[3] = apositiontexturevertex[3].setTexturePosition((float)k / f - f2, (float)l / f1 - f3);
			}

			public void flipFace()
			{
				PositionTextureVertex[] apositiontexturevertex = new PositionTextureVertex[vertexPositions.Length];
				for (int i = 0; i < vertexPositions.Length; i++)
					apositiontexturevertex[i] = vertexPositions[vertexPositions.Length - i - 1];

				vertexPositions = apositiontexturevertex;
			}
		}

		public class TextureOffset
		{

			public int field_40734_a;
			public int field_40733_b;

			public TextureOffset(int i, int j)
			{
				field_40734_a = i;
				field_40733_b = j;
			}
		}

		public class ModelRenderer
		{
			public float textureWidth;
			public float textureHeight;
			private int textureOffsetX;
			private int textureOffsetY;
			public float rotationPointX;
			public float rotationPointY;
			public float rotationPointZ;
			public float rotateAngleX;
			public float rotateAngleY;
			public float rotateAngleZ;
			public bool mirror;
			public bool showModel;
			public bool isHidden;
			public List<ModelBox> cubeList;
			public List<ModelRenderer> childModels;
			public string boxName;
			private ModelBase baseModel;

			public VisiblePartFlags Flags;
			public bool Helmet;
			public bool Animate;

			public ModelRenderer(ModelBase b, VisiblePartFlags flags, bool helmet, bool animate) :
				this(b, null, flags, helmet, animate)
			{
			}

			public ModelRenderer(ModelBase modelbase, string s, VisiblePartFlags flags, bool helmet, bool animate)
			{
				textureWidth = 64F;
				textureHeight = 32F;
				mirror = false;
				showModel = true;
				isHidden = false;
				cubeList = new List<ModelBox>();
				baseModel = modelbase;
				modelbase.boxList.Add(this);
				boxName = s;
				setTextureSize(modelbase.textureWidth, modelbase.textureHeight);

				Flags = flags;
				Helmet = helmet;
				Animate = animate;
			}

			public ModelRenderer(ModelBase modelbase, int i, int j, VisiblePartFlags flags, bool helmet, bool animate) :
				this(modelbase, null, flags, helmet, animate)
			{
				setTextureOffset(i, j);
			}

			public void addChild(ModelRenderer modelrenderer)
			{
				if (childModels == null)
				{
					childModels = new List<ModelRenderer>();
				}
				childModels.Add(modelrenderer);
			}

			public ModelRenderer setTextureOffset(int i, int j)
			{
				textureOffsetX = i;
				textureOffsetY = j;
				return this;
			}

			public ModelRenderer addBox(String s, float f, float f1, float f2, int i, int j, int k)
			{
				s = (new StringBuilder()).Append(boxName).Append(".").Append(s).ToString();
				TextureOffset textureoffset = baseModel.func_40297_a(s);
				setTextureOffset(textureoffset.field_40734_a, textureoffset.field_40733_b);
				cubeList.Add((new ModelBox(this, textureOffsetX, textureOffsetY, f, f1, f2, i, j, k, 0.0F)).func_40671_a(s));
				return this;
			}

			public ModelRenderer addBox(float f, float f1, float f2, int i, int j, int k)
			{
				cubeList.Add(new ModelBox(this, textureOffsetX, textureOffsetY, f, f1, f2, i, j, k, 0.0F));
				return this;
			}

			public void addBox(float f, float f1, float f2, int i, int j, int k, float f3)
			{
				cubeList.Add(new ModelBox(this, textureOffsetX, textureOffsetY, f, f1, f2, i, j, k, f3));
			}

			public void setRotationPoint(float f, float f1, float f2)
			{
				rotationPointX = f;
				rotationPointY = f1;
				rotationPointZ = f2;
			}

			public ModelRenderer setTextureSize(int i, int j)
			{
				textureWidth = i;
				textureHeight = j;
				return this;
			}
		}

		public class PlaneRenderer
		{
			public float textureWidth;
			public float textureHeight;
			public PositionTextureVertex[] corners;
			public TexturedQuad[] faces;
			public int textureOffsetX;
			public int textureOffsetY;
			public float rotationPointX;
			public float rotationPointY;
			public float rotationPointZ;
			public float rotateAngleX;
			public float rotateAngleY;
			public float rotateAngleZ;
			public float field_35977_i;
			public float field_35975_j;
			public float field_35976_k;
			public float field_35973_l;
			public float field_35974_m;
			public float field_35972_n;
			public bool compiled;
			public int displayList;
			public bool mirror;
			public bool showModel;
			public bool isHidden;

			public VisiblePartFlags Flags;
			public bool Helmet;
			public bool Animate, AlsoReverse;

			public PlaneRenderer(ModelBase modelbase, int i, int j, VisiblePartFlags flags, bool helmet, bool animate, bool alsoReverse = true)
			{
				this.textureWidth = 64.0F;
				this.textureHeight = 32.0F;
				this.compiled = false;
				this.displayList = 0;
				this.mirror = false;
				this.showModel = true;
				this.isHidden = false;
				this.textureOffsetX = i;
				this.textureOffsetY = j;
				modelbase.boxList.Add(this);

				Flags = flags;
				Helmet = helmet;
				AlsoReverse = alsoReverse;
				Animate = animate;
			}

			public void addBackPlane(float f, float f1, float f2, int i, int j, int k)
			{
				addBackPlane(f, f1, f2, i, j, k, 0.0F);
			}

			public void addSidePlane(float f, float f1, float f2, int i, int j, int k)
			{
				addSidePlane(f, f1, f2, i, j, k, 0.0F);
			}

			public void addTopPlane(float f, float f1, float f2, int i, int j, int k)
			{
				addTopPlane(f, f1, f2, i, j, k, 0.0F);
			}

			public void addBackPlane(float f, float f1, float f2, int i, int j, int k, float f3)
			{
				this.field_35977_i = f;
				this.field_35975_j = f1;
				this.field_35976_k = f2;
				this.field_35973_l = (f + i);
				this.field_35974_m = (f1 + j);
				this.field_35972_n = (f2 + k);
				this.corners = new PositionTextureVertex[8];
				this.faces = new TexturedQuad[1];
				float f4 = f + i;
				float f5 = f1 + j;
				float f6 = f2 + k;
				f -= f3;
				f1 -= f3;
				f2 -= f3;
				f4 += f3;
				f5 += f3;
				f6 += f3;
				if (this.mirror)
				{
					float f7 = f4;
					f4 = f;
					f = f7;
				}
				PositionTextureVertex positiontexturevertex = new PositionTextureVertex(f, f1, f2, 0.0F, 0.0F);
				PositionTextureVertex positiontexturevertex1 = new PositionTextureVertex(f4, f1, f2, 0.0F, 8.0F);
				PositionTextureVertex positiontexturevertex2 = new PositionTextureVertex(f4, f5, f2, 8.0F, 8.0F);
				PositionTextureVertex positiontexturevertex3 = new PositionTextureVertex(f, f5, f2, 8.0F, 0.0F);
				PositionTextureVertex positiontexturevertex4 = new PositionTextureVertex(f, f1, f6, 0.0F, 0.0F);
				PositionTextureVertex positiontexturevertex5 = new PositionTextureVertex(f4, f1, f6, 0.0F, 8.0F);
				PositionTextureVertex positiontexturevertex6 = new PositionTextureVertex(f4, f5, f6, 8.0F, 8.0F);
				PositionTextureVertex positiontexturevertex7 = new PositionTextureVertex(f, f5, f6, 8.0F, 0.0F);
				this.corners[0] = positiontexturevertex;
				this.corners[1] = positiontexturevertex1;
				this.corners[2] = positiontexturevertex2;
				this.corners[3] = positiontexturevertex3;
				this.corners[4] = positiontexturevertex4;
				this.corners[5] = positiontexturevertex5;
				this.corners[6] = positiontexturevertex6;
				this.corners[7] = positiontexturevertex7;
				this.faces[0] = new TexturedQuad(new PositionTextureVertex[] { positiontexturevertex1, positiontexturevertex, positiontexturevertex3, positiontexturevertex2 }, this.textureOffsetX, this.textureOffsetY, this.textureOffsetX + i, this.textureOffsetY + j, this.textureWidth, this.textureHeight);

				if (this.mirror)
					this.faces[0].flipFace();
			}

			public void addSidePlane(float f, float f1, float f2, int i, int j, int k, float f3)
			{
				this.field_35977_i = f;
				this.field_35975_j = f1;
				this.field_35976_k = f2;
				this.field_35973_l = (f + i);
				this.field_35974_m = (f1 + j);
				this.field_35972_n = (f2 + k);
				this.corners = new PositionTextureVertex[8];
				this.faces = new TexturedQuad[1];
				float f4 = f + i;
				float f5 = f1 + j;
				float f6 = f2 + k;
				f -= f3;
				f1 -= f3;
				f2 -= f3;
				f4 += f3;
				f5 += f3;
				f6 += f3;
				if (this.mirror)
				{
					float f7 = f4;
					f4 = f;
					f = f7;
				}
				PositionTextureVertex positiontexturevertex = new PositionTextureVertex(f, f1, f2, 0.0F, 0.0F);
				PositionTextureVertex positiontexturevertex1 = new PositionTextureVertex(f4, f1, f2, 0.0F, 8.0F);
				PositionTextureVertex positiontexturevertex2 = new PositionTextureVertex(f4, f5, f2, 8.0F, 8.0F);
				PositionTextureVertex positiontexturevertex3 = new PositionTextureVertex(f, f5, f2, 8.0F, 0.0F);
				PositionTextureVertex positiontexturevertex4 = new PositionTextureVertex(f, f1, f6, 0.0F, 0.0F);
				PositionTextureVertex positiontexturevertex5 = new PositionTextureVertex(f4, f1, f6, 0.0F, 8.0F);
				PositionTextureVertex positiontexturevertex6 = new PositionTextureVertex(f4, f5, f6, 8.0F, 8.0F);
				PositionTextureVertex positiontexturevertex7 = new PositionTextureVertex(f, f5, f6, 8.0F, 0.0F);
				this.corners[0] = positiontexturevertex;
				this.corners[1] = positiontexturevertex1;
				this.corners[2] = positiontexturevertex2;
				this.corners[3] = positiontexturevertex3;
				this.corners[4] = positiontexturevertex4;
				this.corners[5] = positiontexturevertex5;
				this.corners[6] = positiontexturevertex6;
				this.corners[7] = positiontexturevertex7;
				this.faces[0] = new TexturedQuad(new PositionTextureVertex[] { positiontexturevertex5, positiontexturevertex1, positiontexturevertex2, positiontexturevertex6 }, this.textureOffsetX, this.textureOffsetY, this.textureOffsetX + k, this.textureOffsetY + j, this.textureWidth, this.textureHeight);

				if (this.mirror)
					this.faces[0].flipFace();
			}

			public void addTopPlane(float f, float f1, float f2, int i, int j, int k, float f3)
			{
				this.field_35977_i = f;
				this.field_35975_j = f1;
				this.field_35976_k = f2;
				this.field_35973_l = (f + i);
				this.field_35974_m = (f1 + j);
				this.field_35972_n = (f2 + k);
				this.corners = new PositionTextureVertex[8];
				this.faces = new TexturedQuad[1];
				float f4 = f + i;
				float f5 = f1 + j;
				float f6 = f2 + k;
				f -= f3;
				f1 -= f3;
				f2 -= f3;
				f4 += f3;
				f5 += f3;
				f6 += f3;
				if (this.mirror)
				{
					float f7 = f4;
					f4 = f;
					f = f7;
				}
				PositionTextureVertex positiontexturevertex = new PositionTextureVertex(f, f1, f2, 0.0F, 0.0F);
				PositionTextureVertex positiontexturevertex1 = new PositionTextureVertex(f4, f1, f2, 0.0F, 8.0F);
				PositionTextureVertex positiontexturevertex2 = new PositionTextureVertex(f4, f5, f2, 8.0F, 8.0F);
				PositionTextureVertex positiontexturevertex3 = new PositionTextureVertex(f, f5, f2, 8.0F, 0.0F);
				PositionTextureVertex positiontexturevertex4 = new PositionTextureVertex(f, f1, f6, 0.0F, 0.0F);
				PositionTextureVertex positiontexturevertex5 = new PositionTextureVertex(f4, f1, f6, 0.0F, 8.0F);
				PositionTextureVertex positiontexturevertex6 = new PositionTextureVertex(f4, f5, f6, 8.0F, 8.0F);
				PositionTextureVertex positiontexturevertex7 = new PositionTextureVertex(f, f5, f6, 8.0F, 0.0F);
				this.corners[0] = positiontexturevertex;
				this.corners[1] = positiontexturevertex1;
				this.corners[2] = positiontexturevertex2;
				this.corners[3] = positiontexturevertex3;
				this.corners[4] = positiontexturevertex4;
				this.corners[5] = positiontexturevertex5;
				this.corners[6] = positiontexturevertex6;
				this.corners[7] = positiontexturevertex7;
				this.faces[0] = new TexturedQuad(new PositionTextureVertex[] { positiontexturevertex5, positiontexturevertex4, positiontexturevertex, positiontexturevertex1 }, this.textureOffsetX, this.textureOffsetY, this.textureOffsetX + i, this.textureOffsetY + k, this.textureWidth, this.textureHeight);

				if (this.mirror)
					this.faces[0].flipFace();
			}

			public void setRotationPoint(float f, float f1, float f2)
			{
				this.rotationPointX = f;
				this.rotationPointY = f1;
				this.rotationPointZ = f2;
			}

			public PlaneRenderer setTextureSize(int i, int j)
			{
				this.textureWidth = i;
				this.textureHeight = j;
				return this;
			}
		}

		public class ModelBox
		{

			public PositionTextureVertex[] field_40679_h;
			public TexturedQuad[] field_40680_i;
			public float field_40678_a;
			public float field_40676_b;
			public float field_40677_c;
			public float field_40674_d;
			public float field_40675_e;
			public float field_40672_f;
			public String field_40673_g;

			public ModelBox(ModelRenderer modelrenderer, int i, int j, float f, float f1, float f2, int k,
					int l, int i1, float f3)
			{
				field_40678_a = f;
				field_40676_b = f1;
				field_40677_c = f2;
				field_40674_d = f + (float)k;
				field_40675_e = f1 + (float)l;
				field_40672_f = f2 + (float)i1;
				field_40679_h = new PositionTextureVertex[8];
				field_40680_i = new TexturedQuad[6];
				float f4 = f + (float)k;
				float f5 = f1 + (float)l;
				float f6 = f2 + (float)i1;
				f -= f3;
				f1 -= f3;
				f2 -= f3;
				f4 += f3;
				f5 += f3;
				f6 += f3;
				if (modelrenderer.mirror)
				{
					float f7 = f4;
					f4 = f;
					f = f7;
				}
				PositionTextureVertex positiontexturevertex = new PositionTextureVertex(f, f1, f2, 0.0F, 0.0F);
				PositionTextureVertex positiontexturevertex1 = new PositionTextureVertex(f4, f1, f2, 0.0F, 8F);
				PositionTextureVertex positiontexturevertex2 = new PositionTextureVertex(f4, f5, f2, 8F, 8F);
				PositionTextureVertex positiontexturevertex3 = new PositionTextureVertex(f, f5, f2, 8F, 0.0F);
				PositionTextureVertex positiontexturevertex4 = new PositionTextureVertex(f, f1, f6, 0.0F, 0.0F);
				PositionTextureVertex positiontexturevertex5 = new PositionTextureVertex(f4, f1, f6, 0.0F, 8F);
				PositionTextureVertex positiontexturevertex6 = new PositionTextureVertex(f4, f5, f6, 8F, 8F);
				PositionTextureVertex positiontexturevertex7 = new PositionTextureVertex(f, f5, f6, 8F, 0.0F);
				field_40679_h[0] = positiontexturevertex;
				field_40679_h[1] = positiontexturevertex1;
				field_40679_h[2] = positiontexturevertex2;
				field_40679_h[3] = positiontexturevertex3;
				field_40679_h[4] = positiontexturevertex4;
				field_40679_h[5] = positiontexturevertex5;
				field_40679_h[6] = positiontexturevertex6;
				field_40679_h[7] = positiontexturevertex7;
				field_40680_i[0] = new TexturedQuad(new PositionTextureVertex[] {
					positiontexturevertex5, positiontexturevertex1, positiontexturevertex2, positiontexturevertex6
				}, i + i1 + k, j + i1, i + i1 + k + i1, j + i1 + l, modelrenderer.textureWidth, modelrenderer.textureHeight);
				field_40680_i[1] = new TexturedQuad(new PositionTextureVertex[] {
					positiontexturevertex, positiontexturevertex4, positiontexturevertex7, positiontexturevertex3
				}, i + 0, j + i1, i + i1, j + i1 + l, modelrenderer.textureWidth, modelrenderer.textureHeight);
				field_40680_i[2] = new TexturedQuad(new PositionTextureVertex[] {
					positiontexturevertex5, positiontexturevertex4, positiontexturevertex, positiontexturevertex1
				}, i + i1, j + 0, i + i1 + k, j + i1, modelrenderer.textureWidth, modelrenderer.textureHeight);
				field_40680_i[3] = new TexturedQuad(new PositionTextureVertex[] {
					positiontexturevertex2, positiontexturevertex3, positiontexturevertex7, positiontexturevertex6
				}, i + i1 + k, j + i1, i + i1 + k + k, j + 0, modelrenderer.textureWidth, modelrenderer.textureHeight);
				field_40680_i[4] = new TexturedQuad(new PositionTextureVertex[] {
					positiontexturevertex1, positiontexturevertex, positiontexturevertex3, positiontexturevertex2
				}, i + i1, j + i1, i + i1 + k, j + i1 + l, modelrenderer.textureWidth, modelrenderer.textureHeight);
				field_40680_i[5] = new TexturedQuad(new PositionTextureVertex[] {
					positiontexturevertex4, positiontexturevertex5, positiontexturevertex6, positiontexturevertex7
				}, i + i1 + k + i1, j + i1, i + i1 + k + i1 + k, j + i1 + l, modelrenderer.textureWidth, modelrenderer.textureHeight);
				if (modelrenderer.mirror)
				{
					for (int j1 = 0; j1 < field_40680_i.Length; j1++)
					{
						field_40680_i[j1].flipFace();
					}

				}
			}

			public ModelBox func_40671_a(String s)
			{
				field_40673_g = s;
				return this;
			}
		}

		public class Entity { }
		public class EntityLiving { }
		public class Map { }

		public abstract class ModelBase
		{

			public float onGround;
			public bool isRiding;
			public List<object> boxList;
			public bool field_40301_k;
			private Dictionary<string, TextureOffset> field_39000_a;
			public int textureWidth;
			public int textureHeight;

			public ModelBase()
			{
				isRiding = false;
				boxList = new List<object>();
				field_40301_k = true;
				field_39000_a = new Dictionary<string, TextureOffset>();
				textureWidth = 64;
				textureHeight = 32;
			}

			protected void setTextureOffset(String s, int i, int j)
			{
				field_39000_a.Add(s, new TextureOffset(i, j));
			}

			public TextureOffset func_40297_a(String s)
			{
				return (TextureOffset)field_39000_a[s];
			}

			public Model Compile(string name, float scale = 1)
			{
				var model = new Model();
				model.Name = name;

				foreach (var boxObj in boxList)
				{
					if (boxObj is ModelRenderer)
					{
						ModelRenderer box = (ModelRenderer)boxObj;
						Mesh mesh = new Mesh("Test");
						mesh.Faces = new List<Face>();
						mesh.Translate = new Vector3(box.rotationPointX, box.rotationPointY, box.rotationPointZ);
						mesh.Helmet = mesh.AllowTransparency = box.Helmet;
						mesh.Part = box.Flags;
						mesh.Rotate = new Vector3(MathHelper.RadiansToDegrees(box.rotateAngleX), MathHelper.RadiansToDegrees(box.rotateAngleY), MathHelper.RadiansToDegrees(box.rotateAngleZ));
						mesh.Pivot = mesh.Translate;

						if (box.Animate)
							mesh.RotateFactor = (box.mirror) ? -25 : 25;

						if (box.Flags == VisiblePartFlags.HelmetFlag || box.Flags == VisiblePartFlags.HeadFlag)
							mesh.FollowCursor = true;

						mesh.Mode = BeginMode.Quads;

						foreach (var face in box.cubeList)
						{
							int[] cwIndices = new int[] { 0, 1, 2, 3 };
							int[] cwwIndices = new int[] { 3, 2, 1, 0 };
							Color4[] colors = new Color4[] { Color4.White, Color4.White, Color4.White, Color4.White };

							if (box.Helmet)
							{
								foreach (var quad in face.field_40680_i)
								{
									List<Vector3> vertices = new List<Vector3>();
									List<Vector2> texcoords = new List<Vector2>();

									foreach (var x in quad.vertexPositions)
									{
										vertices.Add(x.vector3D * scale);
										texcoords.Add(new Vector2(x.texturePositionX, x.texturePositionY));
									}

									Face newFace = new Face(vertices.ToArray(), texcoords.ToArray(), colors, cwwIndices);
									mesh.Faces.Add(newFace);
								}
							}

							foreach (var quad in face.field_40680_i)
							{
								List<Vector3> vertices = new List<Vector3>();
								List<Vector2> texcoords = new List<Vector2>();

								foreach (var x in quad.vertexPositions)
								{
									vertices.Add(x.vector3D * scale);
									texcoords.Add(new Vector2(x.texturePositionX, x.texturePositionY));
								}

								Face newFace = new Face(vertices.ToArray(), texcoords.ToArray(), colors, cwIndices);
								mesh.Faces.Add(newFace);
							}
						}

						model.Meshes.Add(mesh);
					}
					else if (boxObj is PlaneRenderer)
					{
						PlaneRenderer box = (PlaneRenderer)boxObj;

						Mesh mesh = new Mesh("Test");
						mesh.Faces = new List<Face>();
						mesh.Translate = new Vector3(box.rotationPointX, box.rotationPointY, box.rotationPointZ);
						mesh.Helmet = mesh.AllowTransparency = box.Helmet;
						mesh.Part = box.Flags;
						mesh.Rotate = new Vector3(MathHelper.RadiansToDegrees(box.rotateAngleX), MathHelper.RadiansToDegrees(box.rotateAngleY), MathHelper.RadiansToDegrees(box.rotateAngleZ));
						mesh.Pivot = mesh.Translate;

						if (box.Animate)
							mesh.RotateFactor = (box.mirror) ? -25 : 25;

						if (box.Flags == VisiblePartFlags.HelmetFlag || box.Flags == VisiblePartFlags.HeadFlag)
							mesh.FollowCursor = true;

						mesh.Mode = BeginMode.Quads;

						int[] cwIndices = new int[] { 0, 1, 2, 3 };
						int[] cwwIndices = new int[] { 3, 2, 1, 0 };
						Color4[] colors = new Color4[] { Color4.White, Color4.White, Color4.White, Color4.White };

						if (box.Helmet || box.AlsoReverse)
						{
							foreach (var quad in box.faces)
							{
								List<Vector3> vertices = new List<Vector3>();
								List<Vector2> texcoords = new List<Vector2>();

								foreach (var x in quad.vertexPositions)
								{
									vertices.Add(x.vector3D * scale);
									texcoords.Add(new Vector2(x.texturePositionX, x.texturePositionY));
								}

								Face newFace = new Face(vertices.ToArray(), texcoords.ToArray(), colors, cwwIndices);
								mesh.Faces.Add(newFace);
							}
						}

						foreach (var quad in box.faces)
						{
							List<Vector3> vertices = new List<Vector3>();
							List<Vector2> texcoords = new List<Vector2>();

							foreach (var x in quad.vertexPositions)
							{
								vertices.Add(x.vector3D * scale);
								texcoords.Add(new Vector2(x.texturePositionX, x.texturePositionY));
							}

							Face newFace = new Face(vertices.ToArray(), texcoords.ToArray(), colors, cwIndices);
							mesh.Faces.Add(newFace);
						}
	
						model.Meshes.Add(mesh);
					}
				}

				return model;
			}
		}

		public class ModelQuadruped : ModelBase
		{
			public ModelRenderer head;
			public ModelRenderer body;
			public ModelRenderer leg1;
			public ModelRenderer leg2;
			public ModelRenderer leg3;
			public ModelRenderer leg4;
			protected float field_40331_g;
			protected float field_40332_n;

			public ModelQuadruped(int i, float f)
			{
				field_40331_g = 8F;
				field_40332_n = 4F;
				head = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false);
				head.addBox(-4F, -4F, -8F, 8, 8, 8, f);
				head.setRotationPoint(0.0F, 18 - i, -6F);
				body = new ModelRenderer(this, 28, 8, VisiblePartFlags.ChestFlag, false, false);
				body.addBox(-5F, -10F, -7F, 10, 16, 8, f);
				body.setRotationPoint(0.0F, 17 - i, 2.0F);
				body.rotateAngleX = 1.570796F;
				leg1 = new ModelRenderer(this, 0, 16, VisiblePartFlags.LeftLegFlag, false, true);
				leg1.addBox(-2F, 0.0F, -2F, 4, i, 4, f);
				leg1.setRotationPoint(-3F, 24 - i, 7F);
				leg2 = new ModelRenderer(this, 0, 16, VisiblePartFlags.RightLegFlag, false, true);
				leg2.mirror = true;
				leg2.addBox(-2F, 0.0F, -2F, 4, i, 4, f);
				leg2.setRotationPoint(3F, 24 - i, 7F);
				leg3 = new ModelRenderer(this, 0, 16, VisiblePartFlags.LeftArmFlag, false, true);
				leg3.addBox(-2F, 0.0F, -2F, 4, i, 4, f);
				leg3.setRotationPoint(-3F, 24 - i, -5F);
				leg4 = new ModelRenderer(this, 0, 16, VisiblePartFlags.RightArmFlag, false, true);
				leg4.addBox(-2F, 0.0F, -2F, 4, i, 4, f);
				leg4.setRotationPoint(3F, 24 - i, -5F);
				leg4.mirror = true;
			}
		}

		public class ModelPig : ModelQuadruped
		{
			public ModelPig() :
				this(0)
			{
			}

			public ModelPig(float f) :
				base(6, f)
			{
				head.setTextureOffset(16, 16).addBox(-2F, 0.0F, -9F, 4, 3, 1, f);
				field_40331_g = 4F;
			}
		}


		public class ModelBiped : ModelBase
		{

			public ModelRenderer bipedHead;
			public ModelRenderer bipedHeadwear;
			public ModelRenderer bipedBody;
			public ModelRenderer bipedRightArm;
			public ModelRenderer bipedLeftArm;
			public ModelRenderer bipedRightLeg;
			public ModelRenderer bipedLeftLeg;
			public ModelRenderer bipedEars;
			public ModelRenderer bipedCloak;
			public int heldItemLeft;
			public int heldItemRight;
			public bool isSneak;
			public bool aimedBow;

			public ModelBiped() :
				this(0.0F)
			{
			}

			public ModelBiped(float f) :
				this(f, 0.0F)
			{
			}

			public ModelBiped(float f, float f1)
			{
				heldItemLeft = 0;
				heldItemRight = 0;
				isSneak = false;
				aimedBow = false;
				/*bipedCloak = new ModelRenderer(this, 0, 0);
				bipedCloak.addBox(-5F, 0.0F, -1F, 10, 16, 1, f);
				bipedEars = new ModelRenderer(this, 24, 0);
				bipedEars.addBox(-3F, -6F, -1F, 6, 6, 1, f);*/
				bipedHead = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false);
				bipedHead.addBox(-4F, -8F, -4F, 8, 8, 8, f);
				bipedHead.setRotationPoint(0.0F, 0.0F + f1, 0.0F);
				bipedHeadwear = new ModelRenderer(this, 32, 0, VisiblePartFlags.HelmetFlag, true, false);
				bipedHeadwear.addBox(-4F, -8F, -4F, 8, 8, 8, f + 0.5F);
				bipedHeadwear.setRotationPoint(0.0F, 0.0F + f1, 0.0F);
				bipedBody = new ModelRenderer(this, 16, 16, VisiblePartFlags.ChestFlag, false, false);
				bipedBody.addBox(-4F, 0.0F, -2F, 8, 12, 4, f);
				bipedBody.setRotationPoint(0.0F, 0.0F + f1, 0.0F);
				bipedRightArm = new ModelRenderer(this, 40, 16, VisiblePartFlags.RightArmFlag, false, true);
				bipedRightArm.addBox(-3F, -2F, -2F, 4, 12, 4, f);
				bipedRightArm.setRotationPoint(-5F, 2.0F + f1, 0.0F);
				bipedLeftArm = new ModelRenderer(this, 40, 16, VisiblePartFlags.LeftArmFlag, false, true);
				bipedLeftArm.mirror = true;
				bipedLeftArm.addBox(-1F, -2F, -2F, 4, 12, 4, f);
				bipedLeftArm.setRotationPoint(5F, 2.0F + f1, 0.0F);
				bipedRightLeg = new ModelRenderer(this, 0, 16, VisiblePartFlags.RightLegFlag, false, true);
				bipedRightLeg.addBox(-2F, 0.0F, -2F, 4, 12, 4, f);
				bipedRightLeg.setRotationPoint(-2F, 12F + f1, 0.0F);
				bipedLeftLeg = new ModelRenderer(this, 0, 16, VisiblePartFlags.LeftLegFlag, false, true);
				bipedLeftLeg.mirror = true;
				bipedLeftLeg.addBox(-2F, 0.0F, -2F, 4, 12, 4, f);
				bipedLeftLeg.setRotationPoint(2.0F, 12F + f1, 0.0F);
			}
		}

		public class ModelVillager : ModelBase
		{
			public ModelRenderer head;
			public ModelRenderer body;
			public ModelRenderer arms;
			public ModelRenderer field_40336_d;
			public ModelRenderer field_40337_e;
			public int field_40334_f;
			public int field_40335_g;
			public bool field_40341_n;
			public bool field_40342_o;

			public ModelVillager() :
				this(0)
			{
			}

			public ModelVillager(float f) :
				this(f, 0)
			{
			}

			public ModelVillager(float f, float f1)
			{
				field_40334_f = 0;
				field_40335_g = 0;
				field_40341_n = false;
				field_40342_o = false;
				byte byte0 = 64;
				byte byte1 = 64;
				head = (new ModelRenderer(this, VisiblePartFlags.HeadFlag, false, false)).setTextureSize(byte0, byte1);
				head.setRotationPoint(0.0F, 0.0F + f1, 0.0F);
				head.setTextureOffset(0, 0).addBox(-4F, -10F, -4F, 8, 10, 8, f);
				head.setTextureOffset(24, 0).addBox(-1F, -3F, -6F, 2, 4, 2, f);
				arms = (new ModelRenderer(this, VisiblePartFlags.LeftArmFlag, false, false)).setTextureSize(byte0, byte1);
				arms.rotationPointY = 3F;
				arms.rotationPointZ = -1F;
				arms.rotateAngleX = -0.75F;
				arms.setTextureOffset(44, 22).addBox(-8F, -2F, -2F, 4, 8, 4, f);
				arms.setTextureOffset(44, 22).addBox(4F, -2F, -2F, 4, 8, 4, f);
				arms.setTextureOffset(40, 38).addBox(-4F, 2.0F, -2F, 8, 4, 4, f);
				field_40336_d = (new ModelRenderer(this, 0, 22, VisiblePartFlags.LeftLegFlag, false, true)).setTextureSize(byte0, byte1);
				field_40336_d.setRotationPoint(-2F, 12F + f1, 0.0F);
				field_40336_d.addBox(-2F, 0.0F, -2F, 4, 12, 4, f);
				field_40337_e = (new ModelRenderer(this, 0, 22, VisiblePartFlags.RightLegFlag, false, true)).setTextureSize(byte0, byte1);
				field_40337_e.mirror = true;
				field_40337_e.setRotationPoint(2.0F, 12F + f1, 0.0F);
				field_40337_e.addBox(-2F, 0.0F, -2F, 4, 12, 4, f);
				body = (new ModelRenderer(this, VisiblePartFlags.ChestFlag, true, false)).setTextureSize(byte0, byte1);
				body.setRotationPoint(0.0F, 0.0F + f1, 0.0F);
				body.setTextureOffset(16, 20).addBox(-4F, 0.0F, -3F, 8, 12, 6, f);
				body.setTextureOffset(0, 38).addBox(-4F, 0.0F, -3F, 8, 18, 6, f + 0.5F);
			}
		}

		public class ModelCreeper : ModelBase
		{
			public ModelRenderer head;
			public ModelRenderer unusedCreeperHeadwear;
			public ModelRenderer body;
			public ModelRenderer leg1;
			public ModelRenderer leg2;
			public ModelRenderer leg3;
			public ModelRenderer leg4;

			public ModelCreeper() :
				this(0)
			{
			}

			public ModelCreeper(float f)
			{
				int i = 4;
				head = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false);
				head.addBox(-4F, -8F, -4F, 8, 8, 8, f);
				head.setRotationPoint(0.0F, i, 0.0F);
				//unusedCreeperHeadwear = new ModelRenderer(this, 32, 0);
				//unusedCreeperHeadwear.addBox(-4F, -8F, -4F, 8, 8, 8, f + 0.5F);
				//unusedCreeperHeadwear.setRotationPoint(0.0F, i, 0.0F);
				body = new ModelRenderer(this, 16, 16, VisiblePartFlags.ChestFlag, false, false);
				body.addBox(-4F, 0.0F, -2F, 8, 12, 4, f);
				body.setRotationPoint(0.0F, i, 0.0F);
				leg1 = new ModelRenderer(this, 0, 16, VisiblePartFlags.LeftLegFlag, false, true);
				leg1.addBox(-2F, 0.0F, -2F, 4, 6, 4, f);
				leg1.setRotationPoint(-2F, 12 + i, 4F);
				leg2 = new ModelRenderer(this, 0, 16, VisiblePartFlags.RightLegFlag, false, true);
				leg2.addBox(-2F, 0.0F, -2F, 4, 6, 4, f);
				leg2.setRotationPoint(2.0F, 12 + i, 4F);
				leg2.mirror = true;
				leg3 = new ModelRenderer(this, 0, 16, VisiblePartFlags.LeftArmFlag, false, true);
				leg3.addBox(-2F, 0.0F, -2F, 4, 6, 4, f);
				leg3.setRotationPoint(-2F, 12 + i, -4F);
				leg4 = new ModelRenderer(this, 0, 16, VisiblePartFlags.RightArmFlag, false, true);
				leg4.addBox(-2F, 0.0F, -2F, 4, 6, 4, f);
				leg4.setRotationPoint(2.0F, 12 + i, -4F);
				leg4.mirror = true;
			}
		}

		public class ModelCow : ModelQuadruped
		{
			public ModelCow() :
				base(12, 0)
			{
				boxList.Remove(head);
				head = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false);
				head.addBox(-4F, -4F, -6F, 8, 8, 6, 0.0F);
				head.setRotationPoint(0.0F, 4F, -8F);
				head.setTextureOffset(22, 0).addBox(-5F, -5F, -4F, 1, 3, 1, 0.0F);
				head.setTextureOffset(22, 0).addBox(4F, -5F, -4F, 1, 3, 1, 0.0F);
				boxList.Remove(body);
				body = new ModelRenderer(this, 18, 4, VisiblePartFlags.ChestFlag, false, false);
				body.addBox(-6F, -10F, -7F, 12, 18, 10, 0.0F);
				body.setRotationPoint(0.0F, 5F, 2.0F);
				body.setTextureOffset(52, 0).addBox(-2F, 2.0F, -8F, 4, 6, 1);
				body.rotateAngleX = 1.570796F;
				leg1.rotationPointX--;
				leg2.rotationPointX++;
				leg1.rotationPointZ += 0.0F;
				leg2.rotationPointZ += 0.0F;
				leg3.rotationPointX--;
				leg4.rotationPointX++;
				leg3.rotationPointZ--;
				leg4.rotationPointZ--;
				field_40332_n += 2.0F;
			}
		}

		public class ModelChicken : ModelBase
		{
			public ModelRenderer head;
			public ModelRenderer body;
			public ModelRenderer rightLeg;
			public ModelRenderer leftLeg;
			public ModelRenderer rightWing;
			public ModelRenderer leftWing;
			public ModelRenderer bill;
			public ModelRenderer chin;

			public ModelChicken()
			{
				byte byte0 = 16;
				head = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, true, false);
				head.addBox(-2F, -6F, -2F, 4, 6, 3, 0.0F);
				head.setRotationPoint(0.0F, -1 + byte0, -4F);
				bill = new ModelRenderer(this, 14, 0, VisiblePartFlags.HeadFlag, false, false);
				bill.addBox(-2F, -4F, -4F, 4, 2, 2, 0.0F);
				bill.setRotationPoint(0.0F, -1 + byte0, -4F);
				chin = new ModelRenderer(this, 14, 4, VisiblePartFlags.HeadFlag, false, false);
				chin.addBox(-1F, -2F, -3F, 2, 2, 2, 0.0F);
				chin.setRotationPoint(0.0F, -1 + byte0, -4F);
				body = new ModelRenderer(this, 0, 9, VisiblePartFlags.ChestFlag, false, false);
				body.addBox(-3F, -4F, -3F, 6, 8, 6, 0.0F);
				body.setRotationPoint(0.0F, 0 + byte0, 0.0F);
				rightLeg = new ModelRenderer(this, 26, 0, VisiblePartFlags.RightLegFlag, true, true);
				rightLeg.addBox(-1F, 0.0F, -3F, 3, 5, 3);
				rightLeg.setRotationPoint(-2F, 3 + byte0, 1.0F);
				leftLeg = new ModelRenderer(this, 26, 0, VisiblePartFlags.LeftLegFlag, true, true);
				leftLeg.addBox(-1F, 0.0F, -3F, 3, 5, 3);
				leftLeg.setRotationPoint(1.0F, 3 + byte0, 1.0F);
				leftLeg.mirror = true;
				rightWing = new ModelRenderer(this, 24, 13, VisiblePartFlags.RightArmFlag, false, true);
				rightWing.addBox(0.0F, 0.0F, -3F, 1, 4, 6);
				rightWing.setRotationPoint(-4F, -3 + byte0, 0.0F);
				leftWing = new ModelRenderer(this, 24, 13, VisiblePartFlags.LeftArmFlag, false, true);
				leftWing.addBox(-1F, 0.0F, -3F, 1, 4, 6);
				leftWing.setRotationPoint(4F, -3 + byte0, 0.0F);
			}
		}

		public class ModelSlime : ModelBase
		{
			ModelRenderer slimeBodies;
			ModelRenderer slimeRightEye;
			ModelRenderer slimeLeftEye;
			ModelRenderer slimeMouth;

			public ModelSlime(int i)
			{
				if (i > 0)
				{
					slimeBodies = new ModelRenderer(this, 0, 0, VisiblePartFlags.ChestFlag, false, false);
					slimeBodies.addBox(-3F, 17F, -3F, 6, 6, 6);
					slimeRightEye = new ModelRenderer(this, 32, 0, VisiblePartFlags.LeftArmFlag, false, false);
					slimeRightEye.addBox(-3.25F, 18F, -3.5F, 2, 2, 2);
					slimeLeftEye = new ModelRenderer(this, 32, 4, VisiblePartFlags.RightArmFlag, false, false);
					slimeLeftEye.addBox(1.25F, 18F, -3.5F, 2, 2, 2);
					slimeMouth = new ModelRenderer(this, 32, 8, VisiblePartFlags.RightArmFlag, false, false);
					slimeMouth.addBox(0.0F, 21F, -3.5F, 1, 1, 1);
				}
				slimeBodies = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false);
				slimeBodies.addBox(-4F, 16F, -4F, 8, 8, 8);
			}
		}

		public class ModelSquid : ModelBase
		{
			ModelRenderer squidBody;
			ModelRenderer[] squidTentacles;

			public ModelSquid()
			{
				squidTentacles = new ModelRenderer[8];
				var byte0 = -16;
				squidBody = new ModelRenderer(this, 0, 0, VisiblePartFlags.ChestFlag, false, false);
				squidBody.addBox(-6F, -8F, -6F, 12, 16, 12);
				squidBody.rotationPointY += 24 + byte0;
				for (int i = 0; i < squidTentacles.Length; i++)
				{
					squidTentacles[i] = new ModelRenderer(this, 48, 0, VisiblePartFlags.LeftArmFlag, false, true);
					double d = ((double)i * 3.1415926535897931D * 2D) / (double)squidTentacles.Length;
					float f = (float)Math.Cos(d) * 5F;
					float f1 = (float)Math.Sin(d) * 5F;
					squidTentacles[i].addBox(-1F, 0.0F, -1F, 2, 18, 2);
					squidTentacles[i].rotationPointX = f;
					squidTentacles[i].rotationPointZ = f1;
					squidTentacles[i].rotationPointY = 31 + byte0;
					d = ((double)i * 3.1415926535897931D * -2D) / (double)squidTentacles.Length + 1.5707963267948966D;
					squidTentacles[i].rotateAngleY = (float)d;
				}
			}
		}

		public class ModelMagmaCube : ModelBase
		{
			ModelRenderer[] field_40345_a;
			ModelRenderer field_40344_b;

			public ModelMagmaCube()
			{
				field_40345_a = new ModelRenderer[8];
				for (int i = 0; i < field_40345_a.Length; i++)
				{
					byte byte0 = 0;
					int j = i;
					if (i == 2)
					{
						byte0 = 24;
						j = 10;
					}
					else
						if (i == 3)
						{
							byte0 = 24;
							j = 19;
						}
					field_40345_a[i] = new ModelRenderer(this, byte0, j, VisiblePartFlags.HeadFlag, false, false);
					field_40345_a[i].addBox(-4F, 16 + i, -4F, 8, 1, 8);
				}

				field_40344_b = new ModelRenderer(this, 0, 16, VisiblePartFlags.ChestFlag, false, false);
				field_40344_b.addBox(-2F, 18F, -2F, 4, 4, 4);
			}
		}

		public class ModelBlaze : ModelBase
		{
			private ModelRenderer[] field_40323_a;
			private ModelRenderer field_40322_b;

			public ModelBlaze()
			{
				field_40323_a = new ModelRenderer[12];
				for (int i = 0; i < field_40323_a.Length; i++)
				{
					field_40323_a[i] = new ModelRenderer(this, 0, 16, VisiblePartFlags.LeftArmFlag, false, false);
					field_40323_a[i].addBox(0.0F, 0.0F, 0.0F, 2, 8, 2);
				}

				field_40322_b = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false);
				field_40322_b.addBox(-4F, -4F, -4F, 8, 8, 8);

				setRotationAngles(0, 0, 0, 0, 0, 0);
			}

			public void setRotationAngles(float f, float f1, float f2, float f3, float f4, float f5)
			{
				float f6 = f2 * 3.141593F * -0.1F;
				for (int i = 0; i < 4; i++)
				{
					field_40323_a[i].rotationPointY = -2F + (float)Math.Cos(((float)(i * 2) + f2) * 0.25F);
					field_40323_a[i].rotationPointX = (float)Math.Cos(f6) * 9F;
					field_40323_a[i].rotationPointZ = (float)Math.Sin(f6) * 9F;
					f6 += 1.570796F;
				}

				f6 = 0.7853982F + f2 * 3.141593F * 0.03F;
				for (int j = 4; j < 8; j++)
				{
					field_40323_a[j].rotationPointY = 2.0F + (float)Math.Cos(((float)(j * 2) + f2) * 0.25F);
					field_40323_a[j].rotationPointX = (float)Math.Cos(f6) * 7F;
					field_40323_a[j].rotationPointZ = (float)Math.Sin(f6) * 7F;
					f6 += 1.570796F;
				}

				f6 = 0.4712389F + f2 * 3.141593F * -0.05F;
				for (int k = 8; k < 12; k++)
				{
					field_40323_a[k].rotationPointY = 11F + (float)Math.Cos(((float)k * 1.5F + f2) * 0.5F);
					field_40323_a[k].rotationPointX = (float)Math.Cos(f6) * 5F;
					field_40323_a[k].rotationPointZ = (float)Math.Sin(f6) * 5F;
					f6 += 1.570796F;
				}

				field_40322_b.rotateAngleY = f3 / 57.29578F;
				field_40322_b.rotateAngleX = f4 / 57.29578F;
			}
		}

		public class ModelSilverfish : ModelBase
		{

			private ModelRenderer[] silverfishBodyParts;
			private ModelRenderer[] silverfishWings;
			private float[] field_35399_c;
			private static int[,] silverfishBoxLength = {
				{
					3, 2, 2
				}, {
					4, 3, 2
				}, {
					6, 4, 3
				}, {
					3, 3, 3
				}, {
					2, 2, 3
				}, {
					2, 1, 2
				}, {
					1, 1, 2
				}
			};
			private static int[,] silverfishTexturePositions = {
				{
					0, 0
				}, {
					0, 4
				}, {
					0, 9
				}, {
					0, 16
				}, {
					0, 22
				}, {
					11, 0
				}, {
					13, 4
				}
			};

			public ModelSilverfish()
			{
				field_35399_c = new float[7];
				silverfishBodyParts = new ModelRenderer[7];
				float f = -3.5F;
				for (int i = 0; i < silverfishBodyParts.Length; i++)
				{
					silverfishBodyParts[i] = new ModelRenderer(this, silverfishTexturePositions[i, 0], silverfishTexturePositions[i, 1], VisiblePartFlags.ChestFlag, false, false);
					silverfishBodyParts[i].addBox((float)silverfishBoxLength[i, 0] * -0.5F, 0.0F, (float)silverfishBoxLength[i, 2] * -0.5F, silverfishBoxLength[i, 0], silverfishBoxLength[i, 1], silverfishBoxLength[i, 2]);
					silverfishBodyParts[i].setRotationPoint(0.0F, 24 - silverfishBoxLength[i, 1], f);
					field_35399_c[i] = f;
					if (i < silverfishBodyParts.Length - 1)
					{
						f += (float)(silverfishBoxLength[i, 2] + silverfishBoxLength[i + 1, 2]) * 0.5F;
					}
				}

				silverfishWings = new ModelRenderer[3];
				silverfishWings[0] = new ModelRenderer(this, 20, 0, VisiblePartFlags.HeadFlag, true, false);
				silverfishWings[0].addBox(-5F, 0.0F, (float)silverfishBoxLength[2, 2] * -0.5F, 10, 8, silverfishBoxLength[2, 2]);
				silverfishWings[0].setRotationPoint(0.0F, 16F, field_35399_c[2]);
				silverfishWings[1] = new ModelRenderer(this, 20, 11, VisiblePartFlags.HeadFlag, true, false);
				silverfishWings[1].addBox(-3F, 0.0F, (float)silverfishBoxLength[4, 2] * -0.5F, 6, 4, silverfishBoxLength[4, 2]);
				silverfishWings[1].setRotationPoint(0.0F, 20F, field_35399_c[4]);
				silverfishWings[2] = new ModelRenderer(this, 20, 18, VisiblePartFlags.HeadFlag, true, false);
				silverfishWings[2].addBox(-3F, 0.0F, (float)silverfishBoxLength[4, 2] * -0.5F, 6, 5, silverfishBoxLength[1, 2]);
				silverfishWings[2].setRotationPoint(0.0F, 19F, field_35399_c[1]);
			}
		}

		public class ModelEnderman : ModelBiped
		{
			public bool isCarrying;
			public bool isAttacking;

			public ModelEnderman() :
				base(0.0F, -14F)
			{
				isCarrying = false;
				isAttacking = false;
				float f = -14F;
				float f1 = 0.0F;
				bipedHead.Helmet = true;
				boxList.Remove(bipedHeadwear);
				bipedHeadwear = new ModelRenderer(this, 0, 16, VisiblePartFlags.HelmetFlag, true, false);
				bipedHeadwear.addBox(-4F, -8F, -4F, 8, 8, 8, f1 - 0.5F);
				bipedHeadwear.setRotationPoint(0.0F, 0.0F + f, 0.0F);
				boxList.Remove(bipedHead);
				boxList.Add(bipedHead);
				boxList.Remove(bipedBody);
				bipedBody = new ModelRenderer(this, 32, 16, VisiblePartFlags.ChestFlag, false, false);
				bipedBody.addBox(-4F, 0.0F, -2F, 8, 12, 4, f1);
				bipedBody.setRotationPoint(0.0F, 0.0F + f, 0.0F);
				boxList.Remove(bipedRightArm);
				bipedRightArm = new ModelRenderer(this, 56, 0, VisiblePartFlags.RightArmFlag, false, true);
				bipedRightArm.addBox(-1F, -2F, -1F, 2, 30, 2, f1);
				bipedRightArm.setRotationPoint(-5F, 2.0F + f, 0.0F);
				boxList.Remove(bipedLeftArm);
				bipedLeftArm = new ModelRenderer(this, 56, 0, VisiblePartFlags.LeftArmFlag, false, true);
				bipedLeftArm.mirror = true;
				bipedLeftArm.addBox(-1F, -2F, -1F, 2, 30, 2, f1);
				bipedLeftArm.setRotationPoint(5F, 2.0F + f, 0.0F);
				boxList.Remove(bipedRightLeg);
				bipedRightLeg = new ModelRenderer(this, 56, 0, VisiblePartFlags.RightLegFlag, false, true);
				bipedRightLeg.addBox(-1F, 0.0F, -1F, 2, 30, 2, f1);
				bipedRightLeg.setRotationPoint(-2F, 12F + f, 0.0F);
				boxList.Remove(bipedLeftLeg);
				bipedLeftLeg = new ModelRenderer(this, 56, 0, VisiblePartFlags.LeftLegFlag, false, true);
				bipedLeftLeg.mirror = true;
				bipedLeftLeg.addBox(-1F, 0.0F, -1F, 2, 30, 2, f1);
				bipedLeftLeg.setRotationPoint(2.0F, 12F + f, 0.0F);
			}
		}

		public class ModelWolf : ModelBase
		{
			public ModelRenderer wolfHeadMain;
			public ModelRenderer wolfBody;
			public ModelRenderer wolfLeg1;
			public ModelRenderer wolfLeg2;
			public ModelRenderer wolfLeg3;
			public ModelRenderer wolfLeg4;
			ModelRenderer wolfTail;
			ModelRenderer wolfMane;

			public ModelWolf()
			{
				float f = 0.0F;
				float f1 = 13.5F;
				wolfHeadMain = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false);
				wolfHeadMain.addBox(-3F, -3F, -2F, 6, 6, 4, f);
				wolfHeadMain.setRotationPoint(-1F, f1, -7F);
				wolfBody = new ModelRenderer(this, 18, 14, VisiblePartFlags.ChestFlag, false, false);
				wolfBody.addBox(-4F, -2F, -3F, 6, 9, 6, f);
				wolfBody.setRotationPoint(0.0F, 14F, 2.0F);
				wolfMane = new ModelRenderer(this, 21, 0, VisiblePartFlags.HelmetFlag, false, false);
				wolfMane.addBox(-4F, -3F, -3F, 8, 6, 7, f);
				wolfMane.setRotationPoint(-1F, 14F, 2.0F);
				wolfLeg1 = new ModelRenderer(this, 0, 18, VisiblePartFlags.LeftLegFlag, false, true);
				wolfLeg1.addBox(-1F, 0.0F, -1F, 2, 8, 2, f);
				wolfLeg1.setRotationPoint(-2.5F, 16F, 7F);
				wolfLeg2 = new ModelRenderer(this, 0, 18, VisiblePartFlags.RightLegFlag, false, true);
				wolfLeg2.addBox(-1F, 0.0F, -1F, 2, 8, 2, f);
				wolfLeg2.setRotationPoint(0.5F, 16F, 7F);
				wolfLeg3 = new ModelRenderer(this, 0, 18, VisiblePartFlags.LeftArmFlag, false, true);
				wolfLeg3.addBox(-1F, 0.0F, -1F, 2, 8, 2, f);
				wolfLeg3.setRotationPoint(-2.5F, 16F, -4F);
				wolfLeg4 = new ModelRenderer(this, 0, 18, VisiblePartFlags.RightArmFlag, false, true);
				wolfLeg4.addBox(-1F, 0.0F, -1F, 2, 8, 2, f);
				wolfLeg4.setRotationPoint(0.5F, 16F, -4F);
				wolfTail = new ModelRenderer(this, 9, 18, VisiblePartFlags.ChestFlag, false, false);
				wolfTail.addBox(-1F, 0.0F, -1F, 2, 8, 2, f);
				wolfTail.setRotationPoint(-1F, 12F, 8F);
				wolfHeadMain.setTextureOffset(16, 14).addBox(-3F, -5F, 0.0F, 2, 2, 1, f);
				wolfHeadMain.setTextureOffset(16, 14).addBox(1.0F, -5F, 0.0F, 2, 2, 1, f);
				wolfHeadMain.setTextureOffset(0, 10).addBox(-1.5F, 0.0F, -5F, 3, 3, 4, f);

				setLivingAnimations(0, 0, 0);
			}

			public void setLivingAnimations(float f, float f1, float f2)
			{
				wolfBody.setRotationPoint(0.0F, 14F, 2.0F);
				wolfBody.rotateAngleX = 1.570796F;
				wolfMane.setRotationPoint(-1F, 14F, -3F);
				wolfMane.rotateAngleX = wolfBody.rotateAngleX;
				wolfTail.setRotationPoint(-1F, 12F, 8F);
				wolfTail.rotateAngleX = wolfBody.rotateAngleX;
				wolfLeg1.setRotationPoint(-2.5F, 16F, 7F);
				wolfLeg2.setRotationPoint(0.5F, 16F, 7F);
				wolfLeg3.setRotationPoint(-2.5F, 16F, -4F);
				wolfLeg4.setRotationPoint(0.5F, 16F, -4F);
				wolfLeg1.rotateAngleX = (float)Math.Cos(f * 0.6662F) * 1.4F * f1;
				wolfLeg2.rotateAngleX = (float)Math.Cos(f * 0.6662F + 3.141593F) * 1.4F * f1;
				wolfLeg3.rotateAngleX = (float)Math.Cos(f * 0.6662F + 3.141593F) * 1.4F * f1;
				wolfLeg4.rotateAngleX = (float)Math.Cos(f * 0.6662F) * 1.4F * f1;
			}
		}

		public class ModelGhast : ModelBase
		{
			ModelRenderer body;
			ModelRenderer[] tentacles;

			public ModelGhast()
			{
				tentacles = new ModelRenderer[9];
				int byte0 = -16;
				body = new ModelRenderer(this, 0, 0, VisiblePartFlags.ChestFlag, false, false);
				body.addBox(-8F, -8F, -8F, 16, 16, 16);
				body.rotationPointY += 24 + byte0;
				Random random = new Random(1660);
				for (int i = 0; i < tentacles.Length; i++)
				{
					tentacles[i] = new ModelRenderer(this, 0, 0, VisiblePartFlags.LeftLegFlag, false, false);
					float f = (((((float)(i % 3) - (float)((i / 3) % 2) * 0.5F) + 0.25F) / 2.0F) * 2.0F - 1.0F) * 5F;
					float f1 = (((float)(i / 3) / 2.0F) * 2.0F - 1.0F) * 5F;
					int j = random.Next(7) + 8;
					tentacles[i].addBox(-1F, 0.0F, -1F, 2, j, 2);
					tentacles[i].rotationPointX = f;
					tentacles[i].rotationPointZ = f1;
					tentacles[i].rotationPointY = 31 + byte0;
				}

				setRotationAngles(0, 0, 123456, 0, 0, 0);
			}

			public void setRotationAngles(float f, float f1, float f2, float f3, float f4, float f5)
			{
				for (int i = 0; i < tentacles.Length; i++)
				{
					tentacles[i].rotateAngleX = 0.2F * (float)Math.Sin(f2 * 0.3F + (float)i) + 0.4F;
				}
			}
		}

		public class ModelSpider : ModelBase
		{
			public ModelRenderer spiderHead;
			public ModelRenderer spiderNeck;
			public ModelRenderer spiderBody;
			public ModelRenderer spiderLeg1;
			public ModelRenderer spiderLeg2;
			public ModelRenderer spiderLeg3;
			public ModelRenderer spiderLeg4;
			public ModelRenderer spiderLeg5;
			public ModelRenderer spiderLeg6;
			public ModelRenderer spiderLeg7;
			public ModelRenderer spiderLeg8;

			public ModelSpider()
			{
				float f = 0.0F;
				int i = 15;
				spiderHead = new ModelRenderer(this, 32, 4, VisiblePartFlags.HeadFlag, false, false);
				spiderHead.addBox(-4F, -4F, -8F, 8, 8, 8, f);
				spiderHead.setRotationPoint(0.0F, 0 + i, -3F);
				spiderNeck = new ModelRenderer(this, 0, 0, VisiblePartFlags.HelmetFlag, false, false);
				spiderNeck.addBox(-3F, -3F, -3F, 6, 6, 6, f);
				spiderNeck.setRotationPoint(0.0F, i, 0.0F);
				spiderBody = new ModelRenderer(this, 0, 12, VisiblePartFlags.ChestFlag, false, false);
				spiderBody.addBox(-5F, -4F, -6F, 10, 8, 12, f);
				spiderBody.setRotationPoint(0.0F, 0 + i, 9F);
				spiderLeg1 = new ModelRenderer(this, 18, 0, VisiblePartFlags.LeftArmFlag, false, false);
				spiderLeg1.addBox(-15F, -1F, -1F, 16, 2, 2, f);
				spiderLeg1.setRotationPoint(-4F, 0 + i, 2.0F);
				spiderLeg2 = new ModelRenderer(this, 18, 0, VisiblePartFlags.LeftArmFlag, false, true);
				spiderLeg2.addBox(-1F, -1F, -1F, 16, 2, 2, f);
				spiderLeg2.setRotationPoint(4F, 0 + i, 2.0F);
				spiderLeg3 = new ModelRenderer(this, 18, 0, VisiblePartFlags.LeftArmFlag, false, true);
				spiderLeg3.addBox(-15F, -1F, -1F, 16, 2, 2, f);
				spiderLeg3.setRotationPoint(-4F, 0 + i, 1.0F);
				spiderLeg4 = new ModelRenderer(this, 18, 0, VisiblePartFlags.LeftArmFlag, false, true);
				spiderLeg4.addBox(-1F, -1F, -1F, 16, 2, 2, f);
				spiderLeg4.setRotationPoint(4F, 0 + i, 1.0F);
				spiderLeg5 = new ModelRenderer(this, 18, 0, VisiblePartFlags.LeftArmFlag, false, true);
				spiderLeg5.addBox(-15F, -1F, -1F, 16, 2, 2, f);
				spiderLeg5.setRotationPoint(-4F, 0 + i, 0.0F);
				spiderLeg6 = new ModelRenderer(this, 18, 0, VisiblePartFlags.LeftArmFlag, false, true);
				spiderLeg6.addBox(-1F, -1F, -1F, 16, 2, 2, f);
				spiderLeg6.setRotationPoint(4F, 0 + i, 0.0F);
				spiderLeg7 = new ModelRenderer(this, 18, 0, VisiblePartFlags.LeftArmFlag, false, true);
				spiderLeg7.addBox(-15F, -1F, -1F, 16, 2, 2, f);
				spiderLeg7.setRotationPoint(-4F, 0 + i, -1F);
				spiderLeg8 = new ModelRenderer(this, 18, 0, VisiblePartFlags.LeftArmFlag, false, true);
				spiderLeg8.addBox(-1F, -1F, -1F, 16, 2, 2, f);
				spiderLeg8.setRotationPoint(4F, 0 + i, -1F);

				setRotationAngles(0, 0, 0, 0, 0, 0);
			}

			public void setRotationAngles(float f, float f1, float f2, float f3, float f4, float f5)
			{
				spiderHead.rotateAngleY = f3 / 57.29578F;
				spiderHead.rotateAngleX = f4 / 57.29578F;
				float f6 = 0.7853982F;
				spiderLeg1.rotateAngleZ = -f6;
				spiderLeg2.rotateAngleZ = f6;
				spiderLeg3.rotateAngleZ = -f6 * 0.74F;
				spiderLeg4.rotateAngleZ = f6 * 0.74F;
				spiderLeg5.rotateAngleZ = -f6 * 0.74F;
				spiderLeg6.rotateAngleZ = f6 * 0.74F;
				spiderLeg7.rotateAngleZ = -f6;
				spiderLeg8.rotateAngleZ = f6;
				float f7 = -0F;
				float f8 = 0.3926991F;
				spiderLeg1.rotateAngleY = f8 * 2.0F + f7;
				spiderLeg2.rotateAngleY = -f8 * 2.0F - f7;
				spiderLeg3.rotateAngleY = f8 * 1.0F + f7;
				spiderLeg4.rotateAngleY = -f8 * 1.0F - f7;
				spiderLeg5.rotateAngleY = -f8 * 1.0F + f7;
				spiderLeg6.rotateAngleY = f8 * 1.0F - f7;
				spiderLeg7.rotateAngleY = -f8 * 2.0F + f7;
				spiderLeg8.rotateAngleY = f8 * 2.0F - f7;
				float f9 = (float)-(Math.Cos(f * 0.6662F * 2.0F + 0.0F) * 0.4F) * f1;
				float f10 =(float)(-Math.Cos(f * 0.6662F * 2.0F + 3.141593F) * 0.4F) * f1;
				float f11 =(float)(-Math.Cos(f * 0.6662F * 2.0F + 1.570796F) * 0.4F) * f1;
				float f12 =(float)(-Math.Cos(f * 0.6662F * 2.0F + 4.712389F) * 0.4F) * f1;
				float f13 =(float)Math.Abs(Math.Sin(f * 0.6662F + 0.0F) * 0.4F) * f1;
				float f14 =(float)Math.Abs(Math.Sin(f * 0.6662F + 3.141593F) * 0.4F) * f1;
				float f15 =(float)Math.Abs(Math.Sin(f * 0.6662F + 1.570796F) * 0.4F) * f1;
				float f16 =(float)Math.Abs(Math.Sin(f * 0.6662F + 4.712389F) * 0.4F) * f1;
				spiderLeg1.rotateAngleY += f9;
				spiderLeg2.rotateAngleY += -f9;
				spiderLeg3.rotateAngleY += f10;
				spiderLeg4.rotateAngleY += -f10;
				spiderLeg5.rotateAngleY += f11;
				spiderLeg6.rotateAngleY += -f11;
				spiderLeg7.rotateAngleY += f12;
				spiderLeg8.rotateAngleY += -f12;
				spiderLeg1.rotateAngleZ += f13;
				spiderLeg2.rotateAngleZ += -f13;
				spiderLeg3.rotateAngleZ += f14;
				spiderLeg4.rotateAngleZ += -f14;
				spiderLeg5.rotateAngleZ += f15;
				spiderLeg6.rotateAngleZ += -f15;
				spiderLeg7.rotateAngleZ += f16;
				spiderLeg8.rotateAngleZ += -f16;
			}
		}

		public class ModelSheep1 : ModelQuadruped
		{
			public ModelSheep1() :
				base(12, 0.0F)
			{
				boxList.Remove(head);
				head = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false);
				head.addBox(-3F, -4F, -4F, 6, 6, 6, 0.6F);
				head.setRotationPoint(0.0F, 6F, -8F);
				boxList.Remove(body);
				body = new ModelRenderer(this, 28, 8, VisiblePartFlags.ChestFlag, false, false);
				body.addBox(-4F, -10F, -7F, 8, 16, 6, 1.75F);
				body.setRotationPoint(0.0F, 5F, 2.0F);
				body.rotateAngleX = 1.570796F;
				float f = 0.5F;
				boxList.Remove(leg1);
				leg1 = new ModelRenderer(this, 0, 16, VisiblePartFlags.LeftLegFlag, false, true);
				leg1.addBox(-2F, 0.0F, -2F, 4, 6, 4, f);
				leg1.setRotationPoint(-3F, 12F, 7F);
				boxList.Remove(leg2);
				leg2 = new ModelRenderer(this, 0, 16, VisiblePartFlags.RightLegFlag, false, true);
				leg2.addBox(-2F, 0.0F, -2F, 4, 6, 4, f);
				leg2.setRotationPoint(3F, 12F, 7F);
				boxList.Remove(leg3);
				leg3 = new ModelRenderer(this, 0, 16, VisiblePartFlags.LeftArmFlag, false, true);
				leg3.addBox(-2F, 0.0F, -2F, 4, 6, 4, f);
				leg3.setRotationPoint(-3F, 12F, -5F);
				boxList.Remove(leg4);
				leg4 = new ModelRenderer(this, 0, 16, VisiblePartFlags.RightArmFlag, false, true);
				leg4.addBox(-2F, 0.0F, -2F, 4, 6, 4, f);
				leg4.setRotationPoint(3F, 12F, -5F);
			}
		}

		public class ModelSheep2 : ModelQuadruped
		{
			public ModelSheep2() :
				base(12, 0.0F)
			{
				boxList.Remove(head);
				head = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false);
				head.addBox(-3F, -4F, -6F, 6, 6, 8, 0.0F);
				head.setRotationPoint(0.0F, 6F, -8F);
				boxList.Remove(body);
				body = new ModelRenderer(this, 28, 8, VisiblePartFlags.ChestFlag, false, false);
				body.addBox(-4F, -10F, -7F, 8, 16, 6, 0.0F);
				body.setRotationPoint(0.0F, 5F, 2.0F);
				body.rotateAngleX = 1.570796F;
			}
		}

		public class ModelChest : ModelBase
		{
			public ModelRenderer chestLid;
			public ModelRenderer chestBelow;
			public ModelRenderer chestKnob;

			public ModelChest()
			{
				chestLid = (new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, true)).setTextureSize(64, 64);
				chestLid.addBox(0.0F, -5F, -14F, 14, 5, 14, 0.0F);
				chestLid.rotationPointX = 1.0F;
				chestLid.rotationPointY = 7F;
				chestLid.rotationPointZ = 15F;
				chestKnob = (new ModelRenderer(this, 0, 0, VisiblePartFlags.HelmetFlag, false, true)).setTextureSize(64, 64);
				chestKnob.addBox(-1F, -2F, -15F, 2, 4, 1, 0.0F);
				chestKnob.rotationPointX = 8F;
				chestKnob.rotationPointY = 7F;
				chestKnob.rotationPointZ = 15F;
				chestBelow = (new ModelRenderer(this, 0, 19, VisiblePartFlags.ChestFlag, false, false)).setTextureSize(64, 64);
				chestBelow.addBox(0.0F, 0.0F, 0.0F, 14, 10, 14, 0.0F);
				chestBelow.rotationPointX = 1.0F;
				chestBelow.rotationPointY = 6F;
				chestBelow.rotationPointZ = 1.0F;
			}
		}

		public class ModelLargeChest : ModelBase
		{
			public ModelRenderer chestLid;
			public ModelRenderer chestBelow;
			public ModelRenderer chestKnob;

			public ModelLargeChest()
			{
				chestLid = (new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, true)).setTextureSize(128, 64);
				chestLid.addBox(0.0F, -5F, -14F, 30, 5, 14, 0.0F);
				chestLid.rotationPointX = 1.0F;
				chestLid.rotationPointY = 7F;
				chestLid.rotationPointZ = 15F;
				chestKnob = (new ModelRenderer(this, 0, 0, VisiblePartFlags.HelmetFlag, false, true)).setTextureSize(128, 64);
				chestKnob.addBox(-1F, -2F, -15F, 2, 4, 1, 0.0F);
				chestKnob.rotationPointX = 16F;
				chestKnob.rotationPointY = 7F;
				chestKnob.rotationPointZ = 15F;
				chestBelow = (new ModelRenderer(this, 0, 19, VisiblePartFlags.ChestFlag, false, false)).setTextureSize(128, 64);
				chestBelow.addBox(0.0F, 0.0F, 0.0F, 30, 10, 14, 0.0F);
				chestBelow.rotationPointX = 1.0F;
				chestBelow.rotationPointY = 6F;
				chestBelow.rotationPointZ = 1.0F;
			}
		}

		public class ModelBoat : ModelBase
		{
			public ModelRenderer[] boatSides;

			public ModelBoat()
			{
				boatSides = new ModelRenderer[5];
				boatSides[0] = new ModelRenderer(this, 0, 8, VisiblePartFlags.HeadFlag, false, false);
				boatSides[1] = new ModelRenderer(this, 0, 0, VisiblePartFlags.HelmetFlag, false, false);
				boatSides[2] = new ModelRenderer(this, 0, 0, VisiblePartFlags.ChestFlag, false, false);
				boatSides[3] = new ModelRenderer(this, 0, 0, VisiblePartFlags.LeftArmFlag, false, false);
				boatSides[4] = new ModelRenderer(this, 0, 0, VisiblePartFlags.RightArmFlag, false, false);
				byte byte0 = 24;
				byte byte1 = 6;
				byte byte2 = 20;
				byte byte3 = 4;
				boatSides[0].addBox(-byte0 / 2, -byte2 / 2 + 2, -3F, byte0, byte2 - 4, 4, 0.0F);
				boatSides[0].setRotationPoint(0.0F, 0 + byte3, 0.0F);
				boatSides[1].addBox(-byte0 / 2 + 2, -byte1 - 1, -1F, byte0 - 4, byte1, 2, 0.0F);
				boatSides[1].setRotationPoint(-byte0 / 2 + 1, 0 + byte3, 0.0F);
				boatSides[2].addBox(-byte0 / 2 + 2, -byte1 - 1, -1F, byte0 - 4, byte1, 2, 0.0F);
				boatSides[2].setRotationPoint(byte0 / 2 - 1, 0 + byte3, 0.0F);
				boatSides[3].addBox(-byte0 / 2 + 2, -byte1 - 1, -1F, byte0 - 4, byte1, 2, 0.0F);
				boatSides[3].setRotationPoint(0.0F, 0 + byte3, -byte2 / 2 + 1);
				boatSides[4].addBox(-byte0 / 2 + 2, -byte1 - 1, -1F, byte0 - 4, byte1, 2, 0.0F);
				boatSides[4].setRotationPoint(0.0F, 0 + byte3, byte2 / 2 - 1);
				boatSides[0].rotateAngleX = 1.570796F;
				boatSides[1].rotateAngleY = 4.712389F;
				boatSides[2].rotateAngleY = 1.570796F;
				boatSides[3].rotateAngleY = 3.141593F;
			}
		}

		public class SignModel : ModelBase
		{
			public ModelRenderer signBoard;
			public ModelRenderer signStick;

			public SignModel()
			{
				signBoard = new ModelRenderer(this, 0, 0, VisiblePartFlags.HelmetFlag, false, false);
				signBoard.addBox(-12F, -14F, -1F, 24, 12, 2, 0.0F);
				signStick = new ModelRenderer(this, 0, 14, VisiblePartFlags.ChestFlag, false, false);
				signStick.addBox(-1F, -2F, -1F, 2, 14, 2, 0.0F);
			}
		}

		public class ModelBook : ModelBase
		{
			public ModelRenderer field_40330_a;
			public ModelRenderer field_40328_b;
			public ModelRenderer field_40329_c;
			public ModelRenderer field_40326_d;
			public ModelRenderer field_40327_e;
			public ModelRenderer field_40324_f;
			public ModelRenderer field_40325_g;

			public ModelBook()
			{
				field_40330_a = (new ModelRenderer(this, VisiblePartFlags.HeadFlag, false, false)).setTextureOffset(0, 0).addBox(-6F, -5F, 0.0F, 6, 10, 0);
				field_40328_b = (new ModelRenderer(this, VisiblePartFlags.ChestFlag, false, false)).setTextureOffset(16, 0).addBox(0.0F, -5F, 0.0F, 6, 10, 0);
				field_40325_g = (new ModelRenderer(this, VisiblePartFlags.HelmetFlag, false, false)).setTextureOffset(12, 0).addBox(-1F, -5F, 0.0F, 2, 10, 0);
				field_40329_c = (new ModelRenderer(this, VisiblePartFlags.LeftArmFlag, false, false)).setTextureOffset(0, 10).addBox(0.0F, -4F, -0.99F, 5, 8, 1);
				field_40326_d = (new ModelRenderer(this, VisiblePartFlags.LeftLegFlag, false, false)).setTextureOffset(12, 10).addBox(0.0F, -4F, -0.01F, 5, 8, 1);
				field_40327_e = (new ModelRenderer(this, VisiblePartFlags.RightArmFlag, false, false)).setTextureOffset(24, 10).addBox(0.0F, -4F, 0.0F, 5, 8, 0);
				field_40324_f = (new ModelRenderer(this, VisiblePartFlags.RightLegFlag, false, false)).setTextureOffset(24, 10).addBox(0.0F, -4F, 0.0F, 5, 8, 0);
				field_40330_a.setRotationPoint(0.0F, 0.0F, -1F);
				field_40328_b.setRotationPoint(0.0F, 0.0F, 1.0F);
				field_40325_g.rotateAngleY = 1.570796F;
			}

			public void setRotationAngles(float f, float f1, float f2, float f3, float f4, float f5)
			{
				float f6 = (float)(Math.Sin(f * 0.02F) * 0.1F + 1.25F) * f3;
				field_40330_a.rotateAngleY = 3.141593F + f6;
				field_40328_b.rotateAngleY = -f6;
				field_40329_c.rotateAngleY = f6;
				field_40326_d.rotateAngleY = -f6;
				field_40327_e.rotateAngleY = f6 - f6 * 2.0F * f1;
				field_40324_f.rotateAngleY = f6 - f6 * 2.0F * f2;
				field_40329_c.rotationPointX = (float)Math.Sin(f6);
				field_40326_d.rotationPointX = (float)Math.Sin(f6);
				field_40327_e.rotationPointX = (float)Math.Sin(f6);
				field_40324_f.rotationPointX = (float)Math.Sin(f6);
			}
		}

		public class ModelMinecart : ModelBase
		{
			public ModelRenderer[] sideModels;

			public ModelMinecart()
			{
				sideModels = new ModelRenderer[7];
				sideModels[0] = new ModelRenderer(this, 0, 10, VisiblePartFlags.HeadFlag, false, false);
				sideModels[1] = new ModelRenderer(this, 0, 0, VisiblePartFlags.LeftArmFlag, false, false);
				sideModels[2] = new ModelRenderer(this, 0, 0, VisiblePartFlags.LeftLegFlag, false, false);
				sideModels[3] = new ModelRenderer(this, 0, 0, VisiblePartFlags.RightArmFlag, false, false);
				sideModels[4] = new ModelRenderer(this, 0, 0, VisiblePartFlags.RightLegFlag, false, false);
				sideModels[5] = new ModelRenderer(this, 44, 10, VisiblePartFlags.ChestFlag, false, false);
				byte byte0 = 20;
				byte byte1 = 8;
				byte byte2 = 16;
				byte byte3 = 4;
				sideModels[0].addBox(-byte0 / 2, -byte2 / 2, -1F, byte0, byte2, 2, 0.0F);
				sideModels[0].setRotationPoint(0.0F, 0 + byte3, 0.0F);
				sideModels[5].addBox(-byte0 / 2 + 1, -byte2 / 2 + 1, -1F, byte0 - 2, byte2 - 2, 1, 0.0F);
				sideModels[5].setRotationPoint(0.0F, 0 + byte3, 0.0F);
				sideModels[1].addBox(-byte0 / 2 + 2, -byte1 - 1, -1F, byte0 - 4, byte1, 2, 0.0F);
				sideModels[1].setRotationPoint(-byte0 / 2 + 1, 0 + byte3, 0.0F);
				sideModels[2].addBox(-byte0 / 2 + 2, -byte1 - 1, -1F, byte0 - 4, byte1, 2, 0.0F);
				sideModels[2].setRotationPoint(byte0 / 2 - 1, 0 + byte3, 0.0F);
				sideModels[3].addBox(-byte0 / 2 + 2, -byte1 - 1, -1F, byte0 - 4, byte1, 2, 0.0F);
				sideModels[3].setRotationPoint(0.0F, 0 + byte3, -byte2 / 2 + 1);
				sideModels[4].addBox(-byte0 / 2 + 2, -byte1 - 1, -1F, byte0 - 4, byte1, 2, 0.0F);
				sideModels[4].setRotationPoint(0.0F, 0 + byte3, byte2 / 2 - 1);
				sideModels[0].rotateAngleX = 1.570796F;
				sideModels[1].rotateAngleY = 4.712389F;
				sideModels[2].rotateAngleY = 1.570796F;
				sideModels[3].rotateAngleY = 3.141593F;
				sideModels[5].rotateAngleX = -1.570796F;
			}
		}

		public class ModelEnderCrystal : ModelBase
		{
			private ModelRenderer field_41057_g;
			private ModelRenderer field_41058_h;
			private ModelRenderer field_41059_i;

			public ModelEnderCrystal()
			{
				field_41058_h = new ModelRenderer(this, "glass", VisiblePartFlags.HeadFlag, true, false);
				field_41058_h.setTextureOffset(0, 0).addBox(-4F, -4F, -4F, 8, 8, 8);
				field_41057_g = new ModelRenderer(this, "cube", VisiblePartFlags.HelmetFlag, false, false);
				field_41057_g.setTextureOffset(32, 0).addBox(-4F, -4F, -4F, 8, 8, 8);
				field_41059_i = new ModelRenderer(this, "base", VisiblePartFlags.ChestFlag, false, false);
				field_41059_i.setTextureOffset(0, 16).addBox(-6F, 0.0F, -6F, 12, 4, 12);
			}
		}

		public class ModelSnowMan : ModelBase
		{
			public ModelRenderer field_40306_a;
			public ModelRenderer field_40304_b;
			public ModelRenderer field_40305_c;
			public ModelRenderer field_40302_d;
			public ModelRenderer field_40303_e;

			public ModelSnowMan()
			{
				float f = 4F;
				float f1 = 0.0F;
				field_40305_c = (new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false)).setTextureSize(64, 64);
				field_40305_c.addBox(-4F, -8F, -4F, 8, 8, 8, f1 - 0.5F);
				field_40305_c.setRotationPoint(0.0F, 0.0F + f, 0.0F);
				field_40302_d = (new ModelRenderer(this, 32, 0, VisiblePartFlags.HelmetFlag, false, false)).setTextureSize(64, 64);
				field_40302_d.addBox(-1F, 0.0F, -1F, 12, 2, 2, f1 - 0.5F);
				field_40302_d.setRotationPoint(0.0F, (0.0F + f + 9F) - 7F, 0.0F);
				field_40303_e = (new ModelRenderer(this, 32, 0, VisiblePartFlags.ChestFlag, false, false)).setTextureSize(64, 64);
				field_40303_e.addBox(-1F, 0.0F, -1F, 12, 2, 2, f1 - 0.5F);
				field_40303_e.setRotationPoint(0.0F, (0.0F + f + 9F) - 7F, 0.0F);
				field_40306_a = (new ModelRenderer(this, 0, 16, VisiblePartFlags.LeftArmFlag, false, false)).setTextureSize(64, 64);
				field_40306_a.addBox(-5F, -10F, -5F, 10, 10, 10, f1 - 0.5F);
				field_40306_a.setRotationPoint(0.0F, 0.0F + f + 9F, 0.0F);
				field_40304_b = (new ModelRenderer(this, 0, 36, VisiblePartFlags.RightArmFlag, false, false)).setTextureSize(64, 64);
				field_40304_b.addBox(-6F, -12F, -6F, 12, 12, 12, f1 - 0.5F);
				field_40304_b.setRotationPoint(0.0F, 0.0F + f + 20F, 0.0F);

				setRotationAngles(0, 0, 0, (float)Math.PI / 2, 0, 0);
			}

			public void setRotationAngles(float f, float f1, float f2, float f3, float f4, float f5)
			{
				field_40305_c.rotateAngleY = f3 / 57.29578F;
				field_40305_c.rotateAngleX = f4 / 57.29578F;
				field_40306_a.rotateAngleY = (f3 / 57.29578F) * 0.25F;
				float f6 = (float)Math.Sin(field_40306_a.rotateAngleY);
				float f7 = (float)Math.Cos(field_40306_a.rotateAngleY);
				field_40302_d.rotateAngleZ = 1.0F;
				field_40303_e.rotateAngleZ = -1F;
				field_40302_d.rotateAngleY = 0.0F + field_40306_a.rotateAngleY;
				field_40303_e.rotateAngleY = 3.141593F + field_40306_a.rotateAngleY;
				field_40302_d.rotationPointX = f7 * 5F;
				field_40302_d.rotationPointZ = -f6 * 5F;
				field_40303_e.rotationPointX = -f7 * 5F;
				field_40303_e.rotationPointZ = f6 * 5F;
			}
		}

		public class ModelZombie : ModelBiped
		{
			public ModelZombie()
			{
				setRotationAngles(0, 0, 0, 0, 0, 0);
			}

			public void setRotationAngles(float f, float f1, float f2, float f3, float f4, float f5)
			{
				float f6 = (float)Math.Sin(onGround * 3.141593F);
				float f7 = (float)Math.Sin((1.0F - (1.0F - onGround) * (1.0F - onGround)) * 3.141593F);
				bipedRightArm.rotateAngleZ = 0.0F;
				bipedLeftArm.rotateAngleZ = 0.0F;
				bipedRightArm.rotateAngleY = -(0.1F - f6 * 0.6F);
				bipedLeftArm.rotateAngleY = 0.1F - f6 * 0.6F;
				bipedRightArm.rotateAngleX = -1.570796F;
				bipedLeftArm.rotateAngleX = -1.570796F;
				bipedRightArm.rotateAngleX -= f6 * 1.2F - f7 * 0.4F;
				bipedLeftArm.rotateAngleX -= f6 * 1.2F - f7 * 0.4F;
				bipedRightArm.rotateAngleZ += (float)Math.Cos(f2 * 0.09F) * 0.05F + 0.05F;
				bipedLeftArm.rotateAngleZ -= (float)Math.Cos(f2 * 0.09F) * 0.05F + 0.05F;
				bipedRightArm.rotateAngleX += (float)Math.Sin(f2 * 0.067F) * 0.05F;
				bipedLeftArm.rotateAngleX -= (float)Math.Sin(f2 * 0.067F) * 0.05F;
			}
		}

		public class ModelSkeleton : ModelZombie
		{
			public ModelSkeleton()
			{
				float f = 0.0F;
				boxList.Remove(bipedRightArm);
				bipedRightArm = new ModelRenderer(this, 40, 16, VisiblePartFlags.RightArmFlag, false, false);
				bipedRightArm.addBox(-1F, -2F, -1F, 2, 12, 2, f);
				bipedRightArm.setRotationPoint(-5F, 2.0F, 0.0F);
				boxList.Remove(bipedLeftArm);
				bipedLeftArm = new ModelRenderer(this, 40, 16, VisiblePartFlags.RightArmFlag, false, false);
				bipedLeftArm.mirror = true;
				bipedLeftArm.addBox(-1F, -2F, -1F, 2, 12, 2, f);
				bipedLeftArm.setRotationPoint(5F, 2.0F, 0.0F);
				boxList.Remove(bipedRightLeg);
				bipedRightLeg = new ModelRenderer(this, 0, 16, VisiblePartFlags.RightArmFlag, false, false);
				bipedRightLeg.addBox(-1F, 0.0F, -1F, 2, 12, 2, f);
				bipedRightLeg.setRotationPoint(-2F, 12F, 0.0F);
				boxList.Remove(bipedLeftLeg);
				bipedLeftLeg = new ModelRenderer(this, 0, 16, VisiblePartFlags.RightArmFlag, false, false);
				bipedLeftLeg.mirror = true;
				bipedLeftLeg.addBox(-1F, 0.0F, -1F, 2, 12, 2, f);
				bipedLeftLeg.setRotationPoint(2.0F, 12F, 0.0F);

				setRotationAngles(0, 0, 0, 0, 0, 0);
			}

			public void setRotationAngles(float f, float f1, float f2, float f3, float f4, float f5)
			{
				aimedBow = true;
				base.setRotationAngles(f, f1, f2, f3, f4, f5);
			}
		}

		public class pm_Pony : ModelBase
		{
			private bool rainboom;
			private float WingRotateAngleX;
			private float WingRotateAngleY;
			private float WingRotateAngleZ;
			private float TailRotateAngleY;
			public ModelRenderer head;
			public ModelRenderer[] headpiece;
			public ModelRenderer helmet;
			public ModelRenderer Body;
			public PlaneRenderer[] Bodypiece;
			public ModelRenderer rightarm;
			public ModelRenderer LeftArm;
			public ModelRenderer RightLeg;
			public ModelRenderer LeftLeg;
			public ModelRenderer SteveArm;
			public ModelRenderer unicornarm;
			public PlaneRenderer[] Tail;
			public ModelRenderer[] LeftWing;
			public ModelRenderer[] RightWing;
			public ModelRenderer[] LeftWingExt;
			public ModelRenderer[] RightWingExt;
			public float strech;

			public bool isFlying, isUnicorn, isPegasus;

			public pm_Pony()
			{
			}

			public pm_Pony init(bool unicorn, bool pegasis)
			{
				isUnicorn = unicorn;
				isPegasus = pegasis;
				init(0.0F);
				return this;
			}
			public void init(float yoffset)
			{
				init(yoffset, 0.0F);
			}

			public void init(float yoffset, float init_strech)
			{
				this.strech = init_strech;

				float headR1 = 0.0F;
				float headR2 = 0.0F;
				float headR3 = 0.0F;

				this.head = new ModelRenderer(this, 0, 0, VisiblePartFlags.HeadFlag, false, false);
				this.head.addBox(-4.0F, -4.0F, -6.0F, 8, 8, 8, this.strech);
				this.head.setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.headpiece = new ModelRenderer[3];

				this.headpiece[0] = new ModelRenderer(this, 12, 16, VisiblePartFlags.HelmetFlag, true, false);
				this.headpiece[0].addBox(-4.0F, -6.0F, -1.0F, 2, 2, 2, this.strech);
				this.headpiece[0].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.headpiece[1] = new ModelRenderer(this, 12, 16, VisiblePartFlags.HelmetFlag, true, false);
				this.headpiece[1].addBox(2.0F, -6.0F, -1.0F, 2, 2, 2, this.strech);
				this.headpiece[1].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.headpiece[2] = new ModelRenderer(this, 56, 0, VisiblePartFlags.HelmetFlag, true, false);
				this.headpiece[2].addBox(-0.5F, -10.0F, -4.0F, 1, 4, 1, this.strech);
				this.headpiece[2].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.helmet = new ModelRenderer(this, 32, 0, VisiblePartFlags.HelmetFlag, true, false);
				this.helmet.addBox(-4.0F, -4.0F, -6.0F, 8, 8, 8, this.strech + 0.5F);
				this.helmet.setRotationPoint(headR1, headR2 + yoffset, headR3);

				float BodyR1 = 0.0F;
				float BodyR2 = 0.0F;
				float BodyR3 = 0.0F;

				this.Body = new ModelRenderer(this, 16, 16, VisiblePartFlags.ChestFlag, false, false);
				this.Body.addBox(-4.0F, 4.0F, -2.0F, 8, 8, 4, this.strech);
				this.Body.setRotationPoint(BodyR1, BodyR2 + yoffset, BodyR3);

				this.Bodypiece = new PlaneRenderer[13];

				this.Bodypiece[0] = new PlaneRenderer(this, 24, 0, VisiblePartFlags.RightLegFlag, false, false);
				this.Bodypiece[0].addSidePlane(-4.0F, 4.0F, 2.0F, 0, 8, 8, this.strech);
				this.Bodypiece[0].setRotationPoint(BodyR1, BodyR2 + yoffset, BodyR3);

				this.Bodypiece[1] = new PlaneRenderer(this, 24, 0, VisiblePartFlags.RightLegFlag, false, false);
				this.Bodypiece[1].addSidePlane(4.0F, 4.0F, 2.0F, 0, 8, 8, this.strech);
				this.Bodypiece[1].setRotationPoint(BodyR1, BodyR2 + yoffset, BodyR3);

				this.Bodypiece[2] = new PlaneRenderer(this, 24, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Bodypiece[2].addTopPlane(-4.0F, 4.0F, 2.0F, 8, 0, 8, this.strech);
				this.Bodypiece[2].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.Bodypiece[3] = new PlaneRenderer(this, 24, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Bodypiece[3].addTopPlane(-4.0F, 12.0F, 2.0F, 8, 0, 8, this.strech);
				this.Bodypiece[3].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.Bodypiece[4] = new PlaneRenderer(this, 0, 20, VisiblePartFlags.HeadFlag, false, false);
				this.Bodypiece[4].addSidePlane(-4.0F, 4.0F, 10.0F, 0, 8, 4, this.strech);
				this.Bodypiece[4].setRotationPoint(BodyR1, BodyR2 + yoffset, BodyR3);

				this.Bodypiece[5] = new PlaneRenderer(this, 0, 20, VisiblePartFlags.HeadFlag, false, false);
				this.Bodypiece[5].addSidePlane(4.0F, 4.0F, 10.0F, 0, 8, 4, this.strech);
				this.Bodypiece[5].setRotationPoint(BodyR1, BodyR2 + yoffset, BodyR3);

				this.Bodypiece[6] = new PlaneRenderer(this, 24, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Bodypiece[6].addTopPlane(-4.0F, 4.0F, 10.0F, 8, 0, 4, this.strech);
				this.Bodypiece[6].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.Bodypiece[7] = new PlaneRenderer(this, 24, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Bodypiece[7].addTopPlane(-4.0F, 12.0F, 10.0F, 8, 0, 4, this.strech);
				this.Bodypiece[7].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.Bodypiece[8] = new PlaneRenderer(this, 24, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Bodypiece[8].addBackPlane(-4.0F, 4.0F, 14.0F, 8, 8, 0, this.strech);
				this.Bodypiece[8].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.Bodypiece[9] = new PlaneRenderer(this, 32, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Bodypiece[9].addTopPlane(-1.0F, 10.0F, 8.0F, 2, 0, 6, this.strech);
				this.Bodypiece[9].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.Bodypiece[10] = new PlaneRenderer(this, 32, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Bodypiece[10].addTopPlane(-1.0F, 12.0F, 8.0F, 2, 0, 6, this.strech);
				this.Bodypiece[10].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.Bodypiece[11] = new PlaneRenderer(this, 32, 0, VisiblePartFlags.HeadFlag, false, false);
				this.Bodypiece[11].mirror = true;
				this.Bodypiece[11].addSidePlane(-1.0F, 10.0F, 8.0F, 0, 2, 6, this.strech);
				this.Bodypiece[11].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.Bodypiece[12] = new PlaneRenderer(this, 32, 0, VisiblePartFlags.HeadFlag, false, false);
				this.Bodypiece[12].addSidePlane(1.0F, 10.0F, 8.0F, 0, 2, 6, this.strech);
				this.Bodypiece[12].setRotationPoint(headR1, headR2 + yoffset, headR3);

				this.rightarm = new ModelRenderer(this, 40, 16, VisiblePartFlags.RightArmFlag, false, false);
				this.rightarm.addBox(-2.0F, 4.0F, -2.0F, 4, 12, 4, this.strech);
				this.rightarm.setRotationPoint(-3.0F, 8.0F + yoffset, 0.0F);

				this.LeftArm = new ModelRenderer(this, 40, 16, VisiblePartFlags.LeftArmFlag, false, false);
				this.LeftArm.mirror = true;
				this.LeftArm.addBox(-2.0F, 4.0F, -2.0F, 4, 12, 4, this.strech);
				this.LeftArm.setRotationPoint(3.0F, 8.0F + yoffset, 0.0F);

				this.RightLeg = new ModelRenderer(this, 40, 16, VisiblePartFlags.LeftLegFlag, false, false);
				this.RightLeg.addBox(-2.0F, 4.0F, -2.0F, 4, 12, 4, this.strech);
				this.RightLeg.setRotationPoint(-3.0F, 0.0F + yoffset, 0.0F);

				this.LeftLeg = new ModelRenderer(this, 40, 16, VisiblePartFlags.LeftLegFlag, false, false);
				this.LeftLeg.mirror = true;
				this.LeftLeg.addBox(-2.0F, 4.0F, -2.0F, 4, 12, 4, this.strech);
				this.LeftLeg.setRotationPoint(3.0F, 0.0F + yoffset, 0.0F);

				this.SteveArm = new ModelRenderer(this, 40, 16, VisiblePartFlags.HeadFlag, false, false);
				this.SteveArm.addBox(-3.0F, -2.0F, -2.0F, 4, 12, 4, this.strech);
				this.SteveArm.setRotationPoint(-5.0F, 2.0F + yoffset, 0.0F);
				boxList.Remove(SteveArm);

				this.unicornarm = new ModelRenderer(this, 40, 16, VisiblePartFlags.HeadFlag, false, false);
				this.unicornarm.addBox(-3.0F, -2.0F, -2.0F, 4, 12, 4, this.strech);
				this.unicornarm.setRotationPoint(-5.0F, 2.0F + yoffset, 0.0F);
				boxList.Remove(unicornarm);

				float txf = 0.0F;
				float tyf = 8.0F;
				float tzf = -14.0F;
				float TailR1 = 0.0F - txf;
				float TailR2 = 10.0F - tyf;
				float TailR3 = 0.0F;
				this.Tail = new PlaneRenderer[10];

				this.Tail[0] = new PlaneRenderer(this, 32, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Tail[0].addTopPlane(-2.0F + txf, -7.0F + tyf, 16.0F + tzf, 4, 0, 4, this.strech);
				this.Tail[0].setRotationPoint(TailR1, TailR2 + yoffset, TailR3);

				this.Tail[1] = new PlaneRenderer(this, 32, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Tail[1].addTopPlane(-2.0F + txf, 9.0F + tyf, 16.0F + tzf, 4, 0, 4, this.strech);
				this.Tail[1].setRotationPoint(TailR1, TailR2 + yoffset, TailR3);

				this.Tail[2] = new PlaneRenderer(this, 32, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Tail[2].addBackPlane(-2.0F + txf, -7.0F + tyf, 16.0F + tzf, 4, 8, 0, this.strech);
				this.Tail[2].setRotationPoint(TailR1, TailR2 + yoffset, TailR3);

				this.Tail[3] = new PlaneRenderer(this, 32, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Tail[3].addBackPlane(-2.0F + txf, -7.0F + tyf, 20.0F + tzf, 4, 8, 0, this.strech);
				this.Tail[3].setRotationPoint(TailR1, TailR2 + yoffset, TailR3);

				this.Tail[4] = new PlaneRenderer(this, 32, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Tail[4].addBackPlane(-2.0F + txf, 1.0F + tyf, 16.0F + tzf, 4, 8, 0, this.strech);
				this.Tail[4].setRotationPoint(TailR1, TailR2 + yoffset, TailR3);

				this.Tail[5] = new PlaneRenderer(this, 32, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Tail[5].addBackPlane(-2.0F + txf, 1.0F + tyf, 20.0F + tzf, 4, 8, 0, this.strech);
				this.Tail[5].setRotationPoint(TailR1, TailR2 + yoffset, TailR3);

				this.Tail[6] = new PlaneRenderer(this, 36, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Tail[6].mirror = true;
				this.Tail[6].addSidePlane(2.0F + txf, -7.0F + tyf, 16.0F + tzf, 0, 8, 4, this.strech);
				this.Tail[6].setRotationPoint(TailR1, TailR2 + yoffset, TailR3);

				this.Tail[7] = new PlaneRenderer(this, 36, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Tail[7].addSidePlane(-2.0F + txf, -7.0F + tyf, 16.0F + tzf, 0, 8, 4, this.strech);
				this.Tail[7].setRotationPoint(TailR1, TailR2 + yoffset, TailR3);

				this.Tail[8] = new PlaneRenderer(this, 36, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Tail[8].mirror = true;
				this.Tail[8].addSidePlane(2.0F + txf, 1.0F + tyf, 16.0F + tzf, 0, 8, 4, this.strech);
				this.Tail[8].setRotationPoint(TailR1, TailR2 + yoffset, TailR3);

				this.Tail[9] = new PlaneRenderer(this, 36, 0, VisiblePartFlags.ChestFlag, false, false);
				this.Tail[9].addSidePlane(-2.0F + txf, 1.0F + tyf, 16.0F + tzf, 0, 8, 4, this.strech);
				this.Tail[9].setRotationPoint(TailR1, TailR2 + yoffset, TailR3);

				this.TailRotateAngleY = this.Tail[0].rotateAngleY;
				this.TailRotateAngleY = this.Tail[0].rotateAngleY;

				float WingR1 = 0.0F;

				float WingR2 = 0.0F;
				float WingR3 = 0.0F;

				this.LeftWing = new ModelRenderer[3];

				this.LeftWing[0] = new ModelRenderer(this, 56, 16, VisiblePartFlags.RightLegFlag, true, true);
				this.LeftWing[0].mirror = true;
				this.LeftWing[0].addBox(4.0F, 5.0F, 2.0F, 2, 6, 2, this.strech);
				this.LeftWing[0].setRotationPoint(WingR1, WingR2 + yoffset, WingR3);

				this.LeftWing[1] = new ModelRenderer(this, 56, 16, VisiblePartFlags.RightLegFlag, true, true);
				this.LeftWing[1].mirror = true;
				this.LeftWing[1].addBox(4.0F, 5.0F, 4.0F, 2, 8, 2, this.strech);
				this.LeftWing[1].setRotationPoint(WingR1, WingR2 + yoffset, WingR3);

				this.LeftWing[2] = new ModelRenderer(this, 56, 16, VisiblePartFlags.RightLegFlag, true, true);
				this.LeftWing[2].mirror = true;
				this.LeftWing[2].addBox(4.0F, 5.0F, 6.0F, 2, 6, 2, this.strech);
				this.LeftWing[2].setRotationPoint(WingR1, WingR2 + yoffset, WingR3);
				
				this.RightWing = new ModelRenderer[3];

				this.RightWing[0] = new ModelRenderer(this, 56, 16, VisiblePartFlags.RightLegFlag, true, true);
				this.RightWing[0].addBox(-6.0F, 5.0F, 2.0F, 2, 6, 2, this.strech);
				this.RightWing[0].setRotationPoint(WingR1, WingR2 + yoffset, WingR3);

				this.RightWing[1] = new ModelRenderer(this, 56, 16, VisiblePartFlags.RightLegFlag, true, true);
				this.RightWing[1].addBox(-6.0F, 5.0F, 4.0F, 2, 8, 2, this.strech);
				this.RightWing[1].setRotationPoint(WingR1, WingR2 + yoffset, WingR3);

				this.RightWing[2] = new ModelRenderer(this, 56, 16, VisiblePartFlags.RightLegFlag, true, true);
				this.RightWing[2].addBox(-6.0F, 5.0F, 6.0F, 2, 6, 2, this.strech);
				this.RightWing[2].setRotationPoint(WingR1, WingR2 + yoffset, WingR3);
				
				float LeftWingExtR1 = headR1 + 4.5F;
				float LeftWingExtR2 = headR2 + 5.0F;
				float LeftWingExtR3 = headR3 + 6.0F;

				this.LeftWingExt = new ModelRenderer[7];

				this.LeftWingExt[0] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.LeftWingExt[0].mirror = true;

				this.LeftWingExt[0].addBox(0.0F, 0.0F, 0.0F, 1, 8, 2, this.strech + 0.1F);
				this.LeftWingExt[0].setRotationPoint(LeftWingExtR1, LeftWingExtR2 + yoffset, LeftWingExtR3);

				this.LeftWingExt[1] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.LeftWingExt[1].mirror = true;

				this.LeftWingExt[1].addBox(0.0F, 8.0F, 0.0F, 1, 6, 2, this.strech + 0.1F);
				this.LeftWingExt[1].setRotationPoint(LeftWingExtR1, LeftWingExtR2 + yoffset, LeftWingExtR3);

				this.LeftWingExt[2] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.LeftWingExt[2].mirror = true;
				this.LeftWingExt[2].addBox(0.0F, -1.2F, -0.2F, 1, 8, 2, this.strech - 0.2F);
				this.LeftWingExt[2].setRotationPoint(LeftWingExtR1, LeftWingExtR2 + yoffset, LeftWingExtR3);

				this.LeftWingExt[3] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.LeftWingExt[3].mirror = true;
				this.LeftWingExt[3].addBox(0.0F, 1.8F, 1.3F, 1, 8, 2, this.strech - 0.1F);
				this.LeftWingExt[3].setRotationPoint(LeftWingExtR1, LeftWingExtR2 + yoffset, LeftWingExtR3);

				this.LeftWingExt[4] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.LeftWingExt[4].mirror = true;
				this.LeftWingExt[4].addBox(0.0F, 5.0F, 2.0F, 1, 8, 2, this.strech);
				this.LeftWingExt[4].setRotationPoint(LeftWingExtR1, LeftWingExtR2 + yoffset, LeftWingExtR3);

				this.LeftWingExt[5] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.LeftWingExt[5].mirror = true;

				this.LeftWingExt[5].addBox(0.0F, 0.0F, -0.2F, 1, 6, 2, this.strech + 0.3F);
				this.LeftWingExt[5].setRotationPoint(LeftWingExtR1, LeftWingExtR2 + yoffset, LeftWingExtR3);

				this.LeftWingExt[6] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.LeftWingExt[6].mirror = true;

				this.LeftWingExt[6].addBox(0.0F, 0.0F, 0.2F, 1, 3, 2, this.strech + 0.2F);
				this.LeftWingExt[6].setRotationPoint(LeftWingExtR1, LeftWingExtR2 + yoffset, LeftWingExtR3);

				float RightWingExtR1 = headR1 - 5.5F;
				float RightWingExtR2 = headR2 + 5.0F;
				float RightWingExtR3 = headR3 + 6.0F;

				this.RightWingExt = new ModelRenderer[7];

				this.RightWingExt[0] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.RightWingExt[0].mirror = true;

				this.RightWingExt[0].addBox(0.0F, 0.0F, 0.0F, 1, 8, 2, this.strech + 0.1F);
				this.RightWingExt[0].setRotationPoint(RightWingExtR1, RightWingExtR2 + yoffset, RightWingExtR3);

				this.RightWingExt[1] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.RightWingExt[1].mirror = true;

				this.RightWingExt[1].addBox(0.0F, 8.0F, 0.0F, 1, 6, 2, this.strech + 0.1F);
				this.RightWingExt[1].setRotationPoint(RightWingExtR1, RightWingExtR2 + yoffset, RightWingExtR3);

				this.RightWingExt[2] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.RightWingExt[2].mirror = true;
				this.RightWingExt[2].addBox(0.0F, -1.2F, -0.2F, 1, 8, 2, this.strech - 0.2F);
				this.RightWingExt[2].setRotationPoint(RightWingExtR1, RightWingExtR2 + yoffset, RightWingExtR3);

				this.RightWingExt[3] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.RightWingExt[3].mirror = true;
				this.RightWingExt[3].addBox(0.0F, 1.8F, 1.3F, 1, 8, 2, this.strech - 0.1F);
				this.RightWingExt[3].setRotationPoint(RightWingExtR1, RightWingExtR2 + yoffset, RightWingExtR3);

				this.RightWingExt[4] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.RightWingExt[4].mirror = true;
				this.RightWingExt[4].addBox(0.0F, 5.0F, 2.0F, 1, 8, 2, this.strech);
				this.RightWingExt[4].setRotationPoint(RightWingExtR1, RightWingExtR2 + yoffset, RightWingExtR3);

				this.RightWingExt[5] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.RightWingExt[5].mirror = true;

				this.RightWingExt[5].addBox(0.0F, 0.0F, -0.2F, 1, 6, 2, this.strech + 0.3F);
				this.RightWingExt[5].setRotationPoint(RightWingExtR1, RightWingExtR2 + yoffset, RightWingExtR3);

				this.RightWingExt[6] = new ModelRenderer(this, 56, 19, VisiblePartFlags.RightLegFlag, true, true);
				this.RightWingExt[6].mirror = true;

				this.RightWingExt[6].addBox(0.0F, 0.0F, 0.2F, 1, 3, 2, this.strech + 0.2F);
				this.RightWingExt[6].setRotationPoint(RightWingExtR1, RightWingExtR2 + yoffset, RightWingExtR3);

				this.WingRotateAngleX = this.LeftWingExt[0].rotateAngleX;
				this.WingRotateAngleY = this.LeftWingExt[0].rotateAngleY;
				this.WingRotateAngleZ = this.LeftWingExt[0].rotateAngleZ;

				animate(0, 0, 0, 0, 0);
			}

			public void animate(float Move, float Moveswing, float Loop, float Right, float Down)
			{
				float SwingProgress = 0;

				this.rainboom = false;
				float headRotateAngleX;
				float headRotateAngleY;

				//if (this.isSleeping)
				//{
				//  float headRotateAngleY = 1.4F;
				//  headRotateAngleX = 0.1F;
				//} else {
				headRotateAngleY = Right / 57.29578F;
				headRotateAngleX = Down / 57.29578F;
				//}
				this.head.rotateAngleY = headRotateAngleY;
				this.head.rotateAngleX = headRotateAngleX;
				this.headpiece[0].rotateAngleY = headRotateAngleY;
				this.headpiece[0].rotateAngleX = headRotateAngleX;
				this.headpiece[1].rotateAngleY = headRotateAngleY;
				this.headpiece[1].rotateAngleX = headRotateAngleX;
				this.headpiece[2].rotateAngleY = headRotateAngleY;
				this.headpiece[2].rotateAngleX = headRotateAngleX;
				this.helmet.rotateAngleY = headRotateAngleY;
				this.helmet.rotateAngleX = headRotateAngleX;

				this.headpiece[2].rotateAngleX = (headRotateAngleX + 0.5F);
				float rightarmRotateAngleX;
				float LeftArmRotateAngleX;
				float RightLegRotateAngleX;
				float LeftLegRotateAngleX;

				if ((!this.isFlying) || (!this.isPegasus))
				{
					rightarmRotateAngleX = (Move * 0.6662F + 3.141593F) * 0.6F * Moveswing;
					LeftArmRotateAngleX = (Move * 0.6662F) * 0.6F * Moveswing;
					RightLegRotateAngleX = (Move * 0.6662F) * 0.3F * Moveswing;
					LeftLegRotateAngleX = (Move * 0.6662F + 3.141593F) * 0.3F * Moveswing;
					this.rightarm.rotateAngleY = 0.0F;
					this.SteveArm.rotateAngleY = 0.0F;
					this.unicornarm.rotateAngleY = 0.0F;
					this.LeftArm.rotateAngleY = 0.0F;
					this.RightLeg.rotateAngleY = 0.0F;
					this.LeftLeg.rotateAngleY = 0.0F;
				}
				else
				{
				  if (Moveswing < 0.9999F)
				  {
					this.rainboom = false;
					rightarmRotateAngleX = (0.0F - Moveswing * 0.5F);
					LeftArmRotateAngleX = (0.0F - Moveswing * 0.5F);
					RightLegRotateAngleX = (Moveswing * 0.5F);
					LeftLegRotateAngleX = (Moveswing * 0.5F);
				  }
				  else
				  {
					this.rainboom = true;
					rightarmRotateAngleX = 4.712F;
					LeftArmRotateAngleX = 4.712F;
					RightLegRotateAngleX = 1.571F;
					LeftLegRotateAngleX = 1.571F;
				  }
				  this.rightarm.rotateAngleY = 0.2F;
				  this.SteveArm.rotateAngleY = 0.2F;
				  this.LeftArm.rotateAngleY = -0.2F;
				  this.RightLeg.rotateAngleY = -0.2F;
				  this.LeftLeg.rotateAngleY = 0.2F;
				}

				/*if (this.isSleeping) {
				  rightarmRotateAngleX = 4.712F;
				  LeftArmRotateAngleX = 4.712F;
				  RightLegRotateAngleX = 1.571F;
				  LeftLegRotateAngleX = 1.571F;
				}*/

				this.rightarm.rotateAngleX = rightarmRotateAngleX;
				this.SteveArm.rotateAngleX = rightarmRotateAngleX;

				this.unicornarm.rotateAngleX = 0.0F;
				this.LeftArm.rotateAngleX = LeftArmRotateAngleX;
				this.RightLeg.rotateAngleX = RightLegRotateAngleX;
				this.LeftLeg.rotateAngleX = LeftLegRotateAngleX;
				this.rightarm.rotateAngleZ = 0.0F;
				this.SteveArm.rotateAngleZ = 0.0F;
				this.unicornarm.rotateAngleZ = 0.0F;
				this.LeftArm.rotateAngleZ = 0.0F;

				for (int i = 0; i < this.Tail.Length; i++)
				{
				  if (this.rainboom)
					this.Tail[i].rotateAngleZ = 0.0F;
				  else {
					this.Tail[i].rotateAngleZ = ((Move * 0.8F) * 0.2F * Moveswing);
				  }
				}

				/*if ((this.heldItemRight != 0) && (!this.rainboom))
				{
				  if (!this.isUnicorn)
				  {
					this.rightarm.rotateAngleX = (this.rightarm.f * 0.5F - 0.314159F);
					this.SteveArm.rotateAngleX = (this.SteveArm.f * 0.5F - 0.314159F);
				  }
				}*/

				float BodyRotateAngleY = 0.0F;

				if ((SwingProgress > -9990.0 && !this.isUnicorn))
				{
					BodyRotateAngleY = ((SwingProgress) * 3.141593F * 2.0F) * 0.2F;
				}

				this.Body.rotateAngleY = (float)(BodyRotateAngleY * 0.2D);
				for (int i = 0; i < this.Bodypiece.Length; i++)
				{
					this.Bodypiece[i].rotateAngleY = (float)(BodyRotateAngleY * 0.2D);
				}
				for (int i = 0; i < this.LeftWing.Length; i++)
				{
					this.LeftWing[i].rotateAngleY = (float)(BodyRotateAngleY * 0.2D);
				}
				for (int i = 0; i < this.RightWing.Length; i++)
				{
					this.RightWing[i].rotateAngleY = (float)(BodyRotateAngleY * 0.2D);
				}

				for (int i = 0; i < this.Tail.Length; i++)
				{
				  this.Tail[i].rotateAngleY = BodyRotateAngleY;
				}

				float ArmRotationPointZ = (this.Body.rotationPointZ) * 5.0F;
				float ArmRotationPointX = (this.Body.rotationPointX) * 5.0F;
				float LegSplay = 4.0F;
				/*if ((this.issneak) && (!this.isFlying))
				{
				  LegSplay = 0.0F;
				}
				if (this.isSleeping)
				{
				  LegSplay = 2.6F;
				}
				if (this.rainboom) {
				  this.rightarm.rotationPointZ = (ArmRotationPointZ + 2.0F);
				  this.SteveArm.rotationPointZ = (ArmRotationPointZ + 2.0F);
				  this.LeftArm.rotationPointZ = (0.0F - ArmRotationPointZ + 2.0F);
				} else {*/
				this.rightarm.rotationPointZ = (ArmRotationPointZ + 1.0F);
				this.SteveArm.rotationPointZ = (ArmRotationPointZ + 1.0F);
				this.LeftArm.rotationPointZ = (0.0F - ArmRotationPointZ + 1.0F);
				//}
				this.rightarm.rotationPointX = (0.0F - ArmRotationPointX - 1.0F + LegSplay);
				this.SteveArm.rotationPointX = (0.0F - ArmRotationPointX);
				this.LeftArm.rotationPointX = (ArmRotationPointX + 1.0F - LegSplay);
				this.RightLeg.rotationPointX = (0.0F - ArmRotationPointX - 1.0F + LegSplay);
				this.LeftLeg.rotationPointX = (ArmRotationPointX + 1.0F - LegSplay);

				this.rightarm.rotateAngleY += this.Body.rotateAngleY;
				this.LeftArm.rotateAngleY += this.Body.rotateAngleY;
				this.LeftArm.rotateAngleX += this.Body.rotateAngleX;

				this.rightarm.rotationPointY = 8.0F;
				this.LeftArm.rotationPointY = 8.0F;
				this.RightLeg.rotationPointY = 4.0F;
				this.LeftLeg.rotationPointY = 4.0F;

				/*if (SwingProgress > -9990.0F)
				{
				  float f = SwingProgress;
				  f = 1.0F - SwingProgress;
				  f *= f * f;
				  f = 1.0F - f;
				  float f1 = me.a(f * 3.141593F);
				  float SwingProgressPi = me.a(SwingProgress * 3.141593F);
				  float f2 = SwingProgressPi * -(this.head.f - 0.7F) * 0.75F;

				  if (this.isUnicorn)
				  {
					acf tmp1252_1249 = this.unicornarm; tmp1252_1249.rotateAngleX = (float)(tmp1252_1249.f - (f1 * 1.2D + f2));
					this.unicornarm.g += this.Body.g * 2.0F;
					this.unicornarm.rotateAngleZ = (SwingProgressPi * -0.4F);
				  }
				  else
				  {
					acf tmp1313_1310 = this.rightarm; tmp1313_1310.rotateAngleX = (float)(tmp1313_1310.f - (f1 * 1.2D + f2));
					this.rightarm.g += this.Body.g * 2.0F;
					this.rightarm.rotateAngleZ = (SwingProgressPi * -0.4F);
					acf tmp1371_1368 = this.SteveArm; tmp1371_1368.rotateAngleX = (float)(tmp1371_1368.f - (f1 * 1.2D + f2));
					this.SteveArm.g += this.Body.g * 2.0F;
					this.SteveArm.rotateAngleZ = (SwingProgressPi * -0.4F);
				  }
				}

				if ((this.issneak) && (!this.isFlying))
				{
				  float BodyRotateAngleX = 0.4F;
				  float BodyRotationPointY = 7.0F;
				  float BodyRotationPointZ = -4.0F;
				  this.Body.rotateAngleX = BodyRotateAngleX;
				  this.Body.rotationPointY = BodyRotationPointY;
				  this.Body.rotationPointZ = BodyRotationPointZ;
				  for (int i = 0; i < this.Bodypiece.length; i++)
				  {
					this.Bodypiece[i].rotateAngleX = BodyRotateAngleX;
					this.Bodypiece[i].rotationPointY = BodyRotationPointY;
					this.Bodypiece[i].rotationPointZ = BodyRotationPointZ;
				  }

				  float lwrpy = 3.5F;
				  float lwrpz = 6.0F;

				  for (int i = 0; i < this.LeftWingExt.length; i++)
				  {
					this.LeftWingExt[i].rotateAngleX = (float)(BodyRotateAngleX + 2.356194734573364D);
					this.LeftWingExt[i].rotationPointY = (BodyRotationPointY + lwrpy);
					this.LeftWingExt[i].rotationPointZ = (BodyRotationPointZ + lwrpz);

					this.LeftWingExt[i].rotateAngleX = 2.5F;
					this.LeftWingExt[i].rotateAngleZ = -6.0F;
				  }

				  float rwrpy = 4.5F;
				  float rwrpz = 6.0F;

				  for (int i = 0; i < this.LeftWingExt.length; i++)
				  {
					this.RightWingExt[i].rotateAngleX = (float)(BodyRotateAngleX + 2.356194734573364D);
					this.RightWingExt[i].rotationPointY = (BodyRotationPointY + rwrpy);
					this.RightWingExt[i].rotationPointZ = (BodyRotationPointZ + rwrpz);

					this.RightWingExt[i].rotateAngleX = 2.5F;
					this.RightWingExt[i].rotateAngleZ = 6.0F;
				  }

				  this.RightLeg.f -= 0.0F;
				  this.LeftLeg.f -= 0.0F;
				  this.rightarm.f -= 0.4F;
				  this.SteveArm.f += 0.4F;
				  this.unicornarm.f += 0.4F;
				  this.LeftArm.f -= 0.4F;
				  this.RightLeg.rotationPointZ = 10.0F;
				  this.LeftLeg.rotationPointZ = 10.0F;
				  this.RightLeg.rotationPointY = 7.0F;
				  this.LeftLeg.rotationPointY = 7.0F;
				  float headRotationPointX;
				  float headRotationPointY;
				  float headRotationPointZ;
				  float headRotationPointX;
				  if (this.isSleeping) {
					float headRotationPointY = 2.0F;
					float headRotationPointZ = -1.0F;
					headRotationPointX = 1.0F;
				  } else {
					headRotationPointY = 6.0F;
					headRotationPointZ = -2.0F;
					headRotationPointX = 0.0F;
				  }
				  this.head.rotationPointY = headRotationPointY;
				  this.head.rotationPointZ = headRotationPointZ;
				  this.head.rotationPointX = headRotationPointX;
				  this.helmet.rotationPointY = headRotationPointY;
				  this.helmet.rotationPointZ = headRotationPointZ;
				  this.helmet.rotationPointX = headRotationPointX;
				  this.headpiece[0].rotationPointY = headRotationPointY;
				  this.headpiece[0].rotationPointZ = headRotationPointZ;
				  this.headpiece[0].rotationPointX = headRotationPointX;
				  this.headpiece[1].rotationPointY = headRotationPointY;
				  this.headpiece[1].rotationPointZ = headRotationPointZ;
				  this.headpiece[1].rotationPointX = headRotationPointX;
				  this.headpiece[2].rotationPointY = headRotationPointY;
				  this.headpiece[2].rotationPointZ = headRotationPointZ;
				  this.headpiece[2].rotationPointX = headRotationPointX;

				  float txf = 0.0F;
				  float tyf = 8.0F;
				  float tzf = -14.0F;
				  float TailRotationPointX = 0.0F - txf;
				  float TailRotationPointY = 9.0F - tyf;
				  float TailRotationPointZ = -4.0F - tzf;
				  float TailRotateAngleX = 0.0F;
				  for (int i = 0; i < this.Tail.length; i++)
				  {
					this.Tail[i].rotationPointX = TailRotationPointX;
					this.Tail[i].rotationPointY = TailRotationPointY;
					this.Tail[i].rotationPointZ = TailRotationPointZ;
					this.Tail[i].rotateAngleX = TailRotateAngleX;
				  }

				}
				else
				{*/
				float BodyRotateAngleX = 0.0F;
				float BodyRotationPointY = 0.0F;
				float BodyRotationPointZ = 0.0F;
				this.Body.rotateAngleX = BodyRotateAngleX;
				this.Body.rotationPointY = BodyRotationPointY;
				this.Body.rotationPointZ = BodyRotationPointZ;
				for (int i = 0; i < this.Bodypiece.Length; i++)
				{
					this.Bodypiece[i].rotateAngleX = BodyRotateAngleX;
					this.Bodypiece[i].rotationPointY = BodyRotationPointY;
					this.Bodypiece[i].rotationPointZ = BodyRotationPointZ;
				}

				if (this.isPegasus)
				{
					if (!this.isFlying)
					{
						for (int i = 0; i < this.LeftWing.Length; i++)
						{
							this.LeftWing[i].rotateAngleX = (float)(BodyRotateAngleX + 1.570796489715576D);
							this.LeftWing[i].rotationPointY = (BodyRotationPointY + 13.0F);
							this.LeftWing[i].rotationPointZ = (BodyRotationPointZ - 3.0F);
						}
						for (int i = 0; i < this.RightWing.Length; i++)
						{
							this.RightWing[i].rotateAngleX = (float)(BodyRotateAngleX + 1.570796489715576D);
							this.RightWing[i].rotationPointY = (BodyRotationPointY + 13.0F);
							this.RightWing[i].rotationPointZ = (BodyRotationPointZ - 3.0F);
						}
					}
					else
					{
						float lwrpy = 5.5F;
						float lwrpz = 3.0F;

						for (int i = 0; i < this.LeftWingExt.Length; i++)
						{
							this.LeftWingExt[i].rotateAngleX = (float)(BodyRotateAngleX + 1.570796489715576D);
							this.LeftWingExt[i].rotationPointY = (BodyRotationPointY + lwrpy);
							this.LeftWingExt[i].rotationPointZ = (BodyRotationPointZ + lwrpz);
						}

						float rwrpy = 6.5F;
						float rwrpz = 3.0F;

						for (int i = 0; i < this.RightWingExt.Length; i++)
						{
							this.RightWingExt[i].rotateAngleX = (float)(BodyRotateAngleX + 1.570796489715576D);
							this.RightWingExt[i].rotationPointY = (BodyRotationPointY + rwrpy);
							this.RightWingExt[i].rotationPointZ = (BodyRotationPointZ + rwrpz);
						}
					}
				}

				//  }

				this.RightLeg.rotationPointZ = 10.0F;
				this.LeftLeg.rotationPointZ = 10.0F;
				this.RightLeg.rotationPointY = 8.0F;
				this.LeftLeg.rotationPointY = 8.0F;

				float ArmRotateAngleZ = (Loop * 0.09F) * 0.05F + 0.05F;
				float ArmRotateAngleX = (Loop * 0.067F) * 0.05F;
				this.SteveArm.rotateAngleZ += ArmRotateAngleZ;
				this.unicornarm.rotateAngleZ += ArmRotateAngleZ;
				this.SteveArm.rotateAngleX += ArmRotateAngleX;
				this.unicornarm.rotateAngleX += ArmRotateAngleX;

				if ((this.isPegasus) && (this.isFlying))
				{
					this.WingRotateAngleY = ((Loop * 0.067F * 8.0F) * 1.0F);
					this.WingRotateAngleZ = ((Loop * 0.067F * 8.0F) * 1.0F);
					for (int i = 0; i < this.LeftWingExt.Length; i++)
					{
						this.LeftWingExt[i].rotateAngleX = 2.5F;
						this.LeftWingExt[i].rotateAngleZ = (-this.WingRotateAngleZ - 4.712F - 0.4F);
					}
					for (int i = 0; i < this.RightWingExt.Length; i++)
					{
						this.RightWingExt[i].rotateAngleX = 2.5F;
						this.RightWingExt[i].rotateAngleZ = (this.WingRotateAngleZ + 4.712F + 0.4F);
					}
				}

				float headRotationPointX;
				float headRotationPointY;
				float headRotationPointZ;
				/*if (this.isSleeping) {
				  float headRotationPointY = 2.0F;
				  float headRotationPointZ = 1.0F;
				  headRotationPointX = 1.0F;
				} else {*/
				headRotationPointY = 0.0F;
				headRotationPointZ = 0.0F;
				headRotationPointX = 0.0F;
				//}
				this.head.rotationPointY = headRotationPointY;
				this.head.rotationPointZ = headRotationPointZ;
				this.head.rotationPointX = headRotationPointX;
				this.helmet.rotationPointY = headRotationPointY;
				this.helmet.rotationPointZ = headRotationPointZ;
				this.helmet.rotationPointX = headRotationPointX;
				this.headpiece[0].rotationPointY = headRotationPointY;
				this.headpiece[0].rotationPointZ = headRotationPointZ;
				this.headpiece[0].rotationPointX = headRotationPointX;
				this.headpiece[1].rotationPointY = headRotationPointY;
				this.headpiece[1].rotationPointZ = headRotationPointZ;
				this.headpiece[1].rotationPointX = headRotationPointX;
				this.headpiece[2].rotationPointY = headRotationPointY;
				this.headpiece[2].rotationPointZ = headRotationPointZ;
				this.headpiece[2].rotationPointX = headRotationPointX;

				float txf = 0.0F;
				float tyf = 8.0F;
				float tzf = -14.0F;
				float TailRotationPointX = 0.0F - txf;
				float TailRotationPointY = 9.0F - tyf;
				float TailRotationPointZ = 0.0F - tzf;
				float TailRotateAngleX = 0.5F * Moveswing;

				 for (int i = 0; i < this.Tail.Length; i++)
				 {
				   this.Tail[i].rotationPointX = TailRotationPointX;
				   this.Tail[i].rotationPointY = TailRotationPointY;
				   this.Tail[i].rotationPointZ = TailRotationPointZ;
				   if (this.rainboom)
					 this.Tail[i].rotateAngleX = (1.571F + 0.1F * (Move));
				   else {
					 this.Tail[i].rotateAngleX = TailRotateAngleX;
				   }

				 }

				for (int i = 0; i < this.Tail.Length; i++)
				{
				  if (this.rainboom) {
					continue;
				  }
				  this.Tail[i].rotateAngleX += ArmRotateAngleX;
				}

				this.LeftWingExt[2].rotateAngleX -= 0.85F;

				this.LeftWingExt[3].rotateAngleX -= 0.75F;

				this.LeftWingExt[4].rotateAngleX -= 0.5F;

				this.LeftWingExt[6].rotateAngleX -= 0.85F;

				this.RightWingExt[2].rotateAngleX -= 0.85F;

				this.RightWingExt[3].rotateAngleX -= 0.75F;

				this.RightWingExt[4].rotateAngleX -= 0.5F;

				this.RightWingExt[6].rotateAngleX -= 0.85F;

				this.Bodypiece[9].rotateAngleX += 0.5F;
				this.Bodypiece[10].rotateAngleX += 0.5F;
				this.Bodypiece[11].rotateAngleX += 0.5F;
				this.Bodypiece[12].rotateAngleX += 0.5F;

				if (this.rainboom) {
				  for (int i = 0; i < this.Tail.Length; i++)
				  {
					this.Tail[i].rotationPointY += 6.0F;
					this.Tail[i].rotationPointZ += 1.0F;
				  }
				}

				/*if (this.b)
				{
				  float ShiftY = -10.0F;
				  float ShiftZ = -10.0F;

				  this.head.d += ShiftY;
				  this.head.e += ShiftZ;
				  this.headpiece[0].d += ShiftY;
				  this.headpiece[0].e += ShiftZ;
				  this.headpiece[1].d += ShiftY;
				  this.headpiece[1].e += ShiftZ;

				  this.headpiece[2].d += ShiftY;
				  this.headpiece[2].e += ShiftZ;

				  this.helmet.d += ShiftY;
				  this.helmet.e += ShiftZ;
				  this.Body.d += ShiftY;
				  this.Body.e += ShiftZ;
				  for (int i = 0; i < this.Bodypiece.length; i++)
				  {
					this.Bodypiece[i].rotationPointY += ShiftY;
					this.Bodypiece[i].rotationPointZ += ShiftZ;
				  }
				  this.LeftArm.d += ShiftY;
				  this.LeftArm.e += ShiftZ;
				  this.rightarm.d += ShiftY;
				  this.rightarm.e += ShiftZ;
				  this.LeftLeg.d += ShiftY;
				  this.LeftLeg.e += ShiftZ;
				  this.RightLeg.d += ShiftY;
				  this.RightLeg.e += ShiftZ;
				  for (int i = 0; i < this.Tail.length; i++)
				  {
					this.Tail[i].rotationPointY += ShiftY;
					this.Tail[i].rotationPointZ += ShiftZ;
				  }

				  for (int i = 0; i < this.LeftWing.length; i++)
				  {
					this.LeftWing[i].d += ShiftY;
					this.LeftWing[i].e += ShiftZ;
				  }
				  for (int i = 0; i < this.RightWing.length; i++)
				  {
					this.RightWing[i].d += ShiftY;
					this.RightWing[i].e += ShiftZ;
				  }

				  for (int i = 0; i < this.LeftWingExt.length; i++)
				  {
					this.LeftWingExt[i].d += ShiftY;
					this.LeftWingExt[i].e += ShiftZ;
				  }

				  for (int i = 0; i < this.RightWingExt.length; i++)
				  {
					this.RightWingExt[i].d += ShiftY;
					this.RightWingExt[i].e += ShiftZ;
				  }
				}

				if (this.isSleeping)
				{
				  this.rightarm.e += 6.0F;
				  this.LeftArm.e += 6.0F;
				  this.RightLeg.e -= 8.0F;
				  this.LeftLeg.e -= 8.0F;
				  this.rightarm.d += 2.0F;
				  this.LeftArm.d += 2.0F;
				  this.RightLeg.d += 2.0F;
				  this.LeftLeg.d += 2.0F;
				}

				if (this.aimedBow)
				{
				  if (this.isUnicorn) {
					float f7 = 0.0F;
					float f9 = 0.0F;
					this.unicornarm.rotateAngleZ = 0.0F;
					this.unicornarm.rotateAngleY = (-(0.1F - f7 * 0.6F) + this.head.g);
					this.unicornarm.rotateAngleX = (4.712F + this.head.f);
					this.unicornarm.f -= f7 * 1.2F - f9 * 0.4F;
					float f2 = ani.tick;
					this.unicornarm.h += me.b(f2 * 0.09F) * 0.05F + 0.05F;
					this.unicornarm.f += me.a(f2 * 0.067F) * 0.05F;
				  } else {
					float f7 = 0.0F;
					float f9 = 0.0F;
					this.rightarm.rotateAngleZ = 0.0F;
					this.rightarm.rotateAngleY = (-(0.1F - f7 * 0.6F) + this.head.g);
					this.rightarm.rotateAngleX = (4.712F + this.head.f);
					this.rightarm.f -= f7 * 1.2F - f9 * 0.4F;
					float f2 = ani.tick;
					this.rightarm.h += me.b(f2 * 0.09F) * 0.05F + 0.05F;
					this.rightarm.f += me.a(f2 * 0.067F) * 0.05F;

					this.rightarm.e += 1.0F;
				  }
				}*/
			}

			/*
		  public void render(AniParams ani, boolean thirdperson)
		  {
			if (thirdperson)
			{
			  this.head.a(this.scale);

			  this.headpiece[0].a(this.scale);
			  this.headpiece[1].a(this.scale);
			  if (this.isUnicorn)
			  {
				this.headpiece[2].a(this.scale);
			  }

			  this.helmet.a(this.scale);
			  this.Body.a(this.scale);
			  for (int i = 0; i < this.Bodypiece.length; i++)
			  {
				this.Bodypiece[i].render(this.scale);
			  }
			  this.LeftArm.a(this.scale);
			  this.rightarm.a(this.scale);
			  this.LeftLeg.a(this.scale);
			  this.RightLeg.a(this.scale);
			  for (int i = 0; i < this.Tail.length; i++)
			  {
				this.Tail[i].render(this.scale);
			  }

			  if (this.isPegasus)
				if ((this.isFlying) || (this.issneak))
				{
				  for (int i = 0; i < this.LeftWingExt.length; i++)
				  {
					this.LeftWingExt[i].a(this.scale);
				  }
				  for (int i = 0; i < this.RightWingExt.length; i++)
				  {
					this.RightWingExt[i].a(this.scale);
				  }
				}
				else {
				  for (int i = 0; i < this.LeftWing.length; i++)
				  {
					this.LeftWing[i].a(this.scale);
				  }
				  for (int i = 0; i < this.RightWing.length; i++)
				  {
					this.RightWing[i].a(this.scale);
				  }
				}
			}
			else {
			  this.SteveArm.a(this.scale);
			}
		  }

		  public void specials(wb renderman, vi player)
		  {
			if (!this.isSleeping) {
			  if (this.isUnicorn) {
				if (this.aimedBow)
				{
				  renderDrop(renderman, player, this.unicornarm, 1.0F, 0.15F, 0.9375F, 0.0625F);
				}
				else renderDrop(renderman, player, this.unicornarm, 1.0F, 0.35F, 0.5375F, -0.45F);

			  }
			  else
			  {
				renderDrop(renderman, player, this.rightarm, 1.0F, -0.0625F, 0.8375F, 0.0625F);
			  }

			}

			renderPumpkin(renderman, player, this.head, 0.625F, 0.0F, 0.0F, -0.15F);
		  }

		  protected void renderGlow(wb renderman, vi player)
		  {
			dk drop = player.by.a();
			if (drop == null)
			  return;
			GL11.glPushMatrix();
			double d = player.s;
			double d1 = player.t;
			double d2 = player.u;
			GL11.glEnable(32826);
			GL11.glTranslatef((float)d + 0.0F, (float)d1 + 2.3F, (float)d2);

			GL11.glScalef(5.0F, 5.0F, 5.0F);
			GL11.glRotatef(-renderman.i, 0.0F, 1.0F, 0.0F);
			GL11.glRotatef(renderman.j, 1.0F, 0.0F, 0.0F);

			zh renderengine = renderman.e;
			renderengine.b(renderengine.b("/fx/glow.png"));
			cv tessellator = cv.a;

			float f2 = 0.0F;

			float f3 = 0.25F;

			float f4 = 0.0F;

			float f5 = 0.25F;
			float f6 = 1.0F;
			float f7 = 0.5F;
			float f8 = 0.25F;
			tessellator.b();
			tessellator.b(0.0F, 1.0F, 0.0F);

			tessellator.a(-1.0D, -1.0D, 0.0D, 0.0D, 1.0D);
			tessellator.a(-1.0D, 1.0D, 0.0D, 1.0D, 1.0D);
			tessellator.a(1.0D, 1.0D, 0.0D, 1.0D, 0.0D);
			tessellator.a(1.0D, -1.0D, 0.0D, 0.0D, 0.0D);
			tessellator.a();
			GL11.glDisable(32826);
			GL11.glPopMatrix();
		  }*/
		}

		public static void LoadModels()
		{
			new ModelPig().Compile("Pig").Save("Models\\Pig.xml");
			new ModelBiped().Compile("Human").Save("Models\\Human.xml");
			new ModelVillager().Compile("Villager").Save("Models\\Villager.xml");
			new ModelCreeper().Compile("Creeper").Save("Models\\Creeper.xml");
			new ModelCow().Compile("Cow").Save("Models\\Cow.xml");
			new ModelChicken().Compile("Chicken").Save("Models\\Chicken.xml");
			new ModelSlime(0).Compile("Tiny Slime").Save("Models\\TinySlime.xml");
			new ModelSlime(1).Compile("Small Slime", 2).Save("Models\\SmallSlime.xml");
			new ModelSlime(1).Compile("Medium Slime", 3).Save("Models\\MediumSlime.xml");
			new ModelSlime(1).Compile("Huge Slime", 4).Save("Models\\HugeSlime.xml");
			new ModelSquid().Compile("Squid").Save("Models\\Squid.xml");
			new ModelMagmaCube().Compile("Tiny Magma Cube").Save("Models\\TinyMagmaCube.xml");
			new ModelMagmaCube().Compile("Small Magma Cube", 2).Save("Models\\SmallMagmaCube.xml");
			new ModelMagmaCube().Compile("Medium Magma Cube", 3).Save("Models\\MediumMagmaCube.xml");
			new ModelMagmaCube().Compile("Huge Magma Cube", 4).Save("Models\\HugeMagmaCube.xml");
			new ModelBlaze().Compile("Blaze").Save("Models\\Blaze.xml");
			new ModelSilverfish().Compile("Silverfish").Save("Models\\Silverfish.xml");
			new ModelEnderman().Compile("Enderman").Save("Models\\Enderman.xml");
			new ModelWolf().Compile("Wolf").Save("Models\\Wolf.xml");
			new ModelGhast().Compile("Ghast", 1).Save("Models\\Ghast.xml");
			new ModelSpider().Compile("Spider").Save("Models\\Spider.xml");
			new ModelSheep1().Compile("Sheep Fur").Save("Models\\Sheep Fur.xml");
			new ModelSheep2().Compile("Sheep").Save("Models\\Sheep.xml");
			new ModelChest().Compile("Chest").Save("Models\\Chest.xml");
			new ModelLargeChest().Compile("Large Chest").Save("Models\\LargeChest.xml");
			new ModelBoat().Compile("Boat").Save("Models\\Boat.xml");
			new SignModel().Compile("Sign").Save("Models\\Sign.xml");
			new ModelBook().Compile("Book").Save("Models\\Book.xml");
			new ModelMinecart().Compile("Minecart").Save("Models\\Minecart.xml");
			new ModelEnderCrystal().Compile("Ender Crystal").Save("Models\\EnderCrystal.xml");
			new ModelSnowMan().Compile("SnowMan").Save("Models\\SnowMan.xml");
			new pm_Pony().init(false, true).Compile("PonyTest").Save("Models\\PonyTest.xml");
			new ModelZombie().Compile("Zombie").Save("Models\\Zombie.xml");
			new ModelSkeleton().Compile("Skeleton").Save("Models\\Skeleton.xml");

			Directory.CreateDirectory("Models");

			foreach (var m in Directory.GetFiles("Models", "*.xml"))
			{
				try
				{
					Model model = Model.Load(m);
					Models.Add(model.Name, model);
				}
				catch
				{
				}
			}
		}
	}
}