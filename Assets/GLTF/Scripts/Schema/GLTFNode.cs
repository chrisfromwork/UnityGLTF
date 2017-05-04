﻿using UnityEngine;
using System.Collections.Generic;
using GLTF.JsonExtensions;
using Newtonsoft.Json;

namespace GLTF
{
    /// <summary>
    /// A node in the node hierarchy.
    /// When the node contains `skin`, all `mesh.primitives` must contain `JOINT`
    /// and `WEIGHT` attributes.  A node can have either a `matrix` or any combination
    /// of `translation`/`rotation`/`scale` (TRS) properties.
    /// TRS properties are converted to matrices and postmultiplied in
    /// the `T * R * S` order to compose the transformation matrix;
    /// first the scale is applied to the vertices, then the rotation, and then
    /// the translation. If none are provided, the transform is the identity.
    /// When a node is targeted for animation
    /// (referenced by an animation.channel.target), only TRS properties may be present;
    /// `matrix` will not be present.
    /// </summary>
    public class GLTFNode : GLTFChildOfRootProperty
    {

        private bool _useTRS;

        /// <summary>
        /// The index of the camera referenced by this node.
        /// </summary>
        public GLTFCameraId Camera;

        /// <summary>
        /// The indices of this node's children.
        /// </summary>
        public List<GLTFNodeId> Children;

        /// <summary>
        /// The index of the skin referenced by this node.
        /// </summary>
        public GLTFSkinId Skin;

        /// <summary>
        /// A floating-point 4x4 transformation matrix stored in column-major order.
        /// </summary>
        public Matrix4x4 Matrix = Matrix4x4.identity;

        /// <summary>
        /// The index of the mesh in this node.
        /// </summary>
        public GLTFMeshId Mesh;

        /// <summary>
        /// The node's unit quaternion rotation in the order (x, y, z, w),
        /// where w is the scalar.
        /// </summary>
        public Quaternion Rotation = new Quaternion(0, 0, 0, 1);

        /// <summary>
        /// The node's non-uniform scale.
        /// </summary>
        public Vector3 Scale = Vector3.one;

        /// <summary>
        /// The node's translation.
        /// </summary>
        public Vector3 Translation = Vector3.zero;

        /// <summary>
        /// The weights of the instantiated Morph Target.
        /// Number of elements must match number of Morph Targets of used mesh.
        /// </summary>
        public List<double> Weights;

		private static readonly Matrix4x4 InvertZMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));

		public void GetUnityTRSProperties(out Vector3 position, out Quaternion rotation, out Vector3 scale)
	    {
            var mat = Matrix;

			if (_useTRS)
		    {
			    mat = Matrix4x4.TRS(Translation, Rotation, Scale);	
		    }
            

		    mat = InvertZMatrix * mat * InvertZMatrix;

			GetTRSProperties(mat, out position, out rotation, out scale);
		}

        public void SetUnityTransform(Transform transform)
        {
            var unityMat = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
            var gltfMat = InvertZMatrix * unityMat * InvertZMatrix;
            GetTRSProperties(gltfMat, out Translation, out Rotation, out Scale);
        }

        private void GetTRSProperties(Matrix4x4 mat, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
			position = mat.GetColumn(3);

		    scale = new Vector3(
			    mat.GetColumn(0).magnitude,
			    mat.GetColumn(1).magnitude,
			    mat.GetColumn(2).magnitude
		    );

		    var w = Mathf.Sqrt(1.0f + mat.m00 + mat.m11 + mat.m22) / 2.0f;
		    var w4 = 4.0f * w;
		    var x = (mat.m21 - mat.m12) / w4;
		    var y = (mat.m02 - mat.m20) / w4;
		    var z = (mat.m10 - mat.m01) / w4;

		    x = float.IsNaN(x) ? 0 : x;
		    y = float.IsNaN(y) ? 0 : y;
		    z = float.IsNaN(z) ? 0 : z;

		    rotation = new Quaternion(x, y, z, w);
        }

        public static GLTFNode Deserialize(GLTFRoot root, JsonReader reader)
        {
            var node = new GLTFNode();

            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                var curProp = reader.Value.ToString();

                switch (curProp)
                {
                    case "camera":
                        node.Camera = GLTFCameraId.Deserialize(root, reader);
                        break;
                    case "children":
                        node.Children = GLTFNodeId.ReadList(root, reader);
                        break;
                    case "skin":
                        node.Skin = GLTFSkinId.Deserialize(root, reader);
                        break;
                    case "matrix":
                        var list = reader.ReadDoubleList();
                        var mat = new Matrix4x4();
                        for (var i = 0; i < 16; i++)
                        {
                            mat[i] = (float)list[i];
                        }
                        node.Matrix = mat;
                        break;
                    case "mesh":
                        node.Mesh = GLTFMeshId.Deserialize(root, reader);
                        break;
                    case "rotation":
                        node._useTRS = true;
                        node.Rotation = reader.ReadAsQuaternion();
                        break;
                    case "scale":
                        node._useTRS = true;
                        node.Scale = reader.ReadAsVector3();
                        break;
                    case "translation":
                        node._useTRS = true;
                        node.Translation = reader.ReadAsVector3();
                        break;
                    case "weights":
                        node.Weights = reader.ReadDoubleList();
                        break;
					default:
						node.DefaultPropertyDeserializer(root, reader);
						break;
				}
            }

            return node;
        }

        public override void Serialize(JsonWriter writer)
        {
            writer.WriteStartObject();

            if (Camera != null)
            {
                writer.WritePropertyName("camera");
                writer.WriteValue(Camera.Id);
            }

            if (Children != null && Children.Count > 0)
            {
                writer.WritePropertyName("children");
                writer.WriteStartArray();
                foreach (var child in Children)
                {
                    writer.WriteValue(child.Id);
                }
                writer.WriteEndArray();
            }

            if (Skin != null)
            {
                writer.WritePropertyName("skin");
                writer.WriteValue(Skin.Id);
            }

            if (Matrix != null && Matrix != Matrix4x4.identity)
            {
                writer.WritePropertyName("matrix");
                writer.WriteStartArray();
                for (var i = 0; i < 16; i++)
                {
                   writer.WriteValue(Matrix[i]);
                }
                writer.WriteEndArray();
            }

            if (Mesh != null)
            {
                writer.WritePropertyName("mesh");
                writer.WriteValue(Mesh.Id);
            }

            if (Rotation != null && Rotation != Quaternion.identity)
            {
                writer.WritePropertyName("rotation");
                writer.WriteStartArray();
                writer.WriteValue(Rotation.x);
                writer.WriteValue(Rotation.y);
                writer.WriteValue(Rotation.z);
                writer.WriteValue(Rotation.w);
                writer.WriteEndArray();
            }

            if (Scale != null && Scale != Vector3.one)
            {
                writer.WritePropertyName("scale");
                writer.WriteStartArray();
                writer.WriteValue(Scale.x);
                writer.WriteValue(Scale.y);
                writer.WriteValue(Scale.z);
                writer.WriteEndArray();
            }

            if (Translation != null && Translation != Vector3.zero)
            {
                writer.WritePropertyName("translation");
                writer.WriteStartArray();
                writer.WriteValue(Translation.x);
                writer.WriteValue(Translation.y);
                writer.WriteValue(Translation.z);
                writer.WriteEndArray();
            }

            if (Weights != null && Weights.Count > 0)
            {
                writer.WritePropertyName("weights");
                writer.WriteStartArray();
                foreach (var weight in Weights)
                {
                    writer.WriteValue(weight);
                }
                writer.WriteEndArray();
            }

            base.Serialize(writer);
            
            writer.WriteEndObject();
        }
    }
}