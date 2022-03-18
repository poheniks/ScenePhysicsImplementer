using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace ScenePhysicsImplementer
{
    public static class SCEMath
    {
        public static float GlobalMaxNum
        {
            get { return 10000; }
        }
        public static double DegtoRad
        {
            get { return Math.PI / 180; }
        }

        public static double RadtoDeg
        {
            get { return 180 / Math.PI; }
        }

        public static float AlmostZero
        {
            get { return 0.005f; }
        }

        public static int IndexOfAbsMaxVectorComponent(Vec3 vec)
        {
            int index = 0;
            float absMaxValue = 0;
            for(int i = 0; i < 3; i++)
            {
                float value = Math.Abs(vec[i]);
                if (value > absMaxValue)
                {
                    absMaxValue = value;
                    index = i;
                }
            }
            return index;
        }

        public static int IndexOfAbsMinVectorComponent(Vec3 vec)
        {
            int index = 0;
            float absMinValue = 0;
            for (int i = 0; i < 3; i++)
            {
                float value = Math.Abs(vec[i]);
                if (value < absMinValue)
                {
                    absMinValue = value;
                    index = i;
                }
            }
            return index;
        }

        public static Mat3 Mat3InverseComponents(Mat3 mat)
        {
            Mat3 normalizedMat = mat;
            Vec3 normals = normalizedMat.MakeUnit();

            mat.s *= 1 / normals.x;
            mat.f *= 1 / normals.y;
            mat.u *= 1 / normals.z;

            return mat;
        }

        public static Mat3 Mat3Abs(Mat3 mat)
        {
            mat.s = VectorAbs(mat.s);
            mat.f = VectorAbs(mat.f);
            mat.u = VectorAbs(mat.u);
            return mat;
        }

        public static Vec3 AverageVectors(List<Vec3> vectorList)
        {
            int count = vectorList.Count;
            Vec3 sumVectors = new Vec3(0, 0, 0);
            if (count == 0) return sumVectors;

            foreach (Vec3 vector in vectorList)
            {
                sumVectors = new Vec3(sumVectors.x + vector.x, sumVectors.y + vector.y, sumVectors.z + vector.z);
            }
            sumVectors = new Vec3(sumVectors.x / count, sumVectors.y / count, sumVectors.z / count);
            return sumVectors;
        }

        public static System.Numerics.Quaternion SystemQuaternionConvert(Quaternion quat)
        {
            System.Numerics.Quaternion sysQuat = new System.Numerics.Quaternion(quat.X, quat.Y, quat.Z, quat.W);
            return sysQuat;
        }

        public static Quaternion TWQuaternionConvert(System.Numerics.Quaternion quat)
        {
            return new Quaternion(quat.X, quat.Y, quat.Z, quat.W);
        }

        public static Quaternion CreateQuaternionFromTWEulerAngles(Vec3 eulerAngles)
        {
            float yaw = eulerAngles.z;
            float pitch = eulerAngles.x;
            float roll = eulerAngles.y;
            System.Numerics.Quaternion systemQuaternion = System.Numerics.Quaternion.CreateFromYawPitchRoll(roll, pitch, yaw);
            return TWQuaternionConvert(systemQuaternion);
        }

        public static Quaternion QuaternionMultiply(Quaternion quat1, Quaternion quat2)
        {
            System.Numerics.Quaternion sysQuat1 = SystemQuaternionConvert(quat1);
            System.Numerics.Quaternion sysQuat2 = SystemQuaternionConvert(quat2);
            System.Numerics.Quaternion sysQuatOut = sysQuat1 * sysQuat2;
            return new Quaternion(sysQuatOut.X, sysQuatOut.Y, sysQuatOut.Z, sysQuatOut.W);
        }

        public static Quaternion QuaternionDivide(Quaternion quat1, Quaternion quat2)
        {
            System.Numerics.Quaternion sysQuat1 = SystemQuaternionConvert(quat1);
            System.Numerics.Quaternion sysQuat2 = SystemQuaternionConvert(quat2);
            System.Numerics.Quaternion sysQuatOut = System.Numerics.Quaternion.Divide(sysQuat1, sysQuat2);
            return new Quaternion(sysQuatOut.X, sysQuatOut.Y, sysQuatOut.Z, sysQuatOut.W);
        }

        public static Matrix3x3 TransformInertiaTensor(Matrix3x3 tensor, Mat3 initialRotation, Mat3 targetRotation)
        {
            Mat3 transformRotation = new Mat3();
            if (!initialRotation.IsZero()) transformRotation = targetRotation.TransformToParent(initialRotation);
            else transformRotation = targetRotation;

            Matrix3x3 transformationMatrix = Mat3ToMatrix(transformRotation);

            //invert off angle tensors
            float[,] tensorMod = tensor.array;
            
            tensorMod[0, 1] *= -1;
            tensorMod[0, 2] *= -1;
            tensorMod[1, 0] *= -1;
            tensorMod[1, 2] *= -1;
            tensorMod[2, 0] *= -1;
            tensorMod[2, 1] *= -1;
            
            Matrix3x3 newTensor = new Matrix3x3(tensorMod);
            Matrix3x3 transposedTransformationMatrix = transformationMatrix;
            transposedTransformationMatrix.Transpose();

            Matrix3x3 transformedTensor = Matrix3x3.MultiplyBy3x3(transformationMatrix, newTensor);
            transformedTensor = Matrix3x3.MultiplyBy3x3(transformedTensor, transposedTransformationMatrix);
            return transformedTensor;
        }

        public static Matrix3x3 Mat3ToMatrix(Mat3 mat)
        {
            float[,] matrixArray = new float[3, 3]
            {
                { mat.s.x, mat.s.y, mat.s.z },
                { mat.f.x, mat.f.y, mat.f.z },
                { mat.u.x, mat.u.y, mat.u.z }
            };
            return new Matrix3x3(matrixArray);
        }

        public static Mat3 MatrixToMat3(Matrix3x3 matrix)
        {
            float[,] array = matrix.array;
            Vec3 s = new Vec3(array[0, 0], array[0, 1], array[0, 2]);
            Vec3 f = new Vec3(array[1, 0], array[1, 1], array[1, 2]);
            Vec3 u = new Vec3(array[2, 0], array[2, 1], array[2, 2]);
            return new Mat3(s, f, u);
        }

        public static Vec3 ProjectVectorOntoPlane(Vec3 vec, Vec3 a, Vec3 b, Vec3 c)
        {
            Vec3 planeNormal = NormalOfPlane(a, b, c);
            Vec3 projectedVec = vec - ((Vec3.DotProduct(vec, planeNormal) / planeNormal.LengthSquared)*planeNormal);
            return projectedVec;
        }

        public static Vec3 NormalOfPlane(Vec3 a, Vec3 b, Vec3 c)
        {
            Vec3 normal = Vec3.CrossProduct((c - a), (b - a));
            return normal.NormalizedCopy();
        }

        public static Vec3 LimitVectorComponents(Vec3 vec, float limit)
        {
            vec.x = Math.Min(limit, Math.Max(-limit, vec.x));
            vec.y = Math.Min(limit, Math.Max(-limit, vec.y));
            vec.z = Math.Min(limit, Math.Max(-limit, vec.z));

            return vec;
        }

        public static Vec3 VectorTransposeXZY(Vec3 vec)
        {
            return new Vec3(vec.x, vec.z, vec.y);
        }
        public static Vec3 VectorTransposeZXY(Vec3 vec)
        {
            return new Vec3(vec.z, vec.x, vec.y);
        }
        public static Vec3 VectorTransposeZYX(Vec3 vec)
        {
            return new Vec3(vec.z, vec.y, vec.x);
        }
        public static Vec3 VectorTransposeYZX(Vec3 vec)
        {
            return new Vec3(vec.y, vec.z, vec.x);
        }
        public static Vec3 VectorTransposeYXZ(Vec3 vec)
        {
            return new Vec3(vec.y, vec.x, vec.z);
        }

        public static Vec3 VectorRound(Vec3 vec, int decimals)
        {
            float x = (float)Math.Round(vec.x, decimals);
            float y = (float)Math.Round(vec.y, decimals);
            float z = (float)Math.Round(vec.z, decimals);

            return new Vec3(x, y, z);
        }

        public static Vec3 VectorAbs(Vec3 vec)
        {
            vec.x = Math.Abs(vec.x);
            vec.y = Math.Abs(vec.y);
            vec.z = Math.Abs(vec.z);
            return vec;
        }

        public static Vec3 VectorMultiplyComponents(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z);
        }

        public static Vec3 VectorDivideComponents(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.x / v2.x, v1.y / v2.y, v1.z / v2.z);
        }

        public static Vec3 VectorInverseComponents(Vec3 vec)
        {
            return new Vec3(1 / vec.x, 1 / vec.y, 1 / vec.z);
        }

        public static Vec3 VectorSquareRootComponents(Vec3 vec)
        {
            vec.x = signedPower(vec.x, 0.5f);
            vec.y = signedPower(vec.y, 0.5f);
            vec.z = signedPower(vec.z, 0.5f);
            return vec;
        }

        public static Vec3 VectorSquareComponents(Vec3 vec)
        {
            vec.x = signedPower(vec.x, 2f);
            vec.y = signedPower(vec.y, 2f);
            vec.z = signedPower(vec.z, 2f);
            return vec;
        }

        public static float signedPower(float x, float y)
        {
            if (x == 0) return 0f;
            return (float)(Math.Pow(x, y)) * Math.Sign(x);
        }

        public static float Resultant(float a, float b)
        {
            return (float)Math.Sqrt(Math.Pow(a, 2) + Math.Pow(b, 2));
        }

        public static void DebugMessage(string message)
        {
            TaleWorlds.Core.InformationManager.DisplayMessage(new TaleWorlds.Core.InformationMessage(message));
        }

        public struct Matrix2x2
        {
            public float[,] array { get { return GetArray(); } }
            public float determinant { get; private set; }
            public float _00 { get; private set; }
            public float _01 { get; private set; }
            public float _10 { get; private set; }
            public float _11 { get; private set; }

            public Matrix2x2(float[,] _array)
            {
                _00 = _array[0, 0];
                _01 = _array[0, 1];
                _10 = _array[1, 0];
                _11 = _array[1, 1];
                determinant = _00 * _11 - _01 * _10;
            }

            private float[,] GetArray()
            {
                float[,] _array = new float[2,2];
                _array[0, 0] = _00;
                _array[0, 1] = _00;
                _array[1, 0] = _00;
                _array[1, 1] = _00;
                return _array;
            }
        }

        public struct Matrix3x1
        {
            public float[,] array { get { return GetArray(); } }
            public float _0 { get; private set; }
            public float _1 { get; private set; }
            public float _2 { get; private set; }

            public Matrix3x1(float[,] array)
            {
                _0 = array[0,0];
                _1 = array[1,0];
                _2 = array[2,0];
            }
            private float[,] GetArray()
            {
                float[,] _array = new float[3, 1];
                _array[0, 0] = _0;
                _array[1, 0] = _1;
                _array[2, 0] = _2;
                return _array;
            }
        }

        public struct Matrix3x3
        {
            public float[,] array { get { return GetArray(); } }
            public float determinant { get; private set; }
            public float _00 { get; private set; }
            public float _01 { get; private set; }
            public float _02 { get; private set; }
            public float _10 { get; private set; }
            public float _11 { get; private set; }
            public float _12 { get; private set; }
            public float _20 { get; private set; }
            public float _21 { get; private set; }
            public float _22 { get; private set; }


            public Matrix3x3(float[,] array)
            {
                 _00 = array[0, 0];
                 _01 = array[0, 1];
                 _02 = array[0, 2];
                 _10 = array[1, 0];
                 _11 = array[1, 1];
                 _12 = array[1, 2];
                 _20 = array[2, 0];
                 _21 = array[2, 1];
                 _22 = array[2, 2];

                float a = _00 * (_11 * _22 - _12 * _21);
                float b = _01 * (_10 * _22 - _12 * _20);
                float c = _02 * (_10 * _21 - _11 * _20);
                determinant = a - b + c;
            }
            private float[,] GetArray()
            {
                float[,] _array = new float[3, 3];
                _array[0, 0] = _00;
                _array[0, 1] = _01;
                _array[0, 2] = _02;
                _array[1, 0] = _10;
                _array[1, 1] = _11;
                _array[1, 2] = _12;
                _array[2, 0] = _20;
                _array[2, 1] = _21;
                _array[2, 2] = _22;
                return _array;
            }

            public void Transpose()
            {
                float[,] original = array;
                //_11 = original[0, 0];
                _01 = original[1, 0];
                _02 = original[2, 0];
                _10 = original[0, 1];
                //_22 = original[1, 1];
                _12 = original[2, 1];
                _20 = original[0, 2];
                _21 = original[1, 2];
                //_33 = original[2, 2];
            }

            public void AdjugateMatrix()
            {
                float[,] inv = new float[3, 3];

                float inv00 = new Matrix2x2(new float[2, 2] { { _11, _12 }, { _21, _22 } }).determinant;
                float inv01 = new Matrix2x2(new float[2, 2] { { _10, _12 }, { _20, _22 } }).determinant;
                float inv02 = new Matrix2x2(new float[2, 2] { { _10, _11 }, { _20, _21 } }).determinant;
                float inv10 = new Matrix2x2(new float[2, 2] { { _01, _02 }, { _21, _22 } }).determinant;
                float inv11 = new Matrix2x2(new float[2, 2] { { _00, _02 }, { _20, _22 } }).determinant;
                float inv12 = new Matrix2x2(new float[2, 2] { { _00, _01 }, { _20, _21 } }).determinant;
                float inv20 = new Matrix2x2(new float[2, 2] { { _01, _02 }, { _11, _12 } }).determinant;
                float inv21 = new Matrix2x2(new float[2, 2] { { _00, _02 }, { _10, _12 } }).determinant;
                float inv22 = new Matrix2x2(new float[2, 2] { { _00, _01 }, { _10, _11 } }).determinant;

                _00 = inv00;
                _01 = inv01;
                _02 = inv02;
                _10 = inv10;
                _11 = inv11;
                _12 = inv12;
                _20 = inv20;
                _21 = inv21;
                _22 = inv22;
            } 

            public void MultiplyByCofactors()
            {
                _01 *= -1;
                _10 *= -1;
                _12 *= -1;
                _21 *= -1;
            }

            public static Matrix3x3 InvertedCopy(Matrix3x3 matrix)
            {
                if (matrix.determinant == 0) return new Matrix3x3();
                Matrix3x3 copyMatrix = matrix;
                

                copyMatrix.Transpose();
                copyMatrix.AdjugateMatrix();
                copyMatrix.MultiplyByCofactors();
                float[,] copyArray = copyMatrix.array;

                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        copyArray[i,j] *= (1 / matrix.determinant);
                    }
                }
                return new Matrix3x3(copyArray);
            }
            public Matrix3x1 MultiplyBy3x1(Matrix3x1 matrix)
            {
                float i = (_00 * matrix._0) + (_01 * matrix._1) + (_02 * matrix._2);
                float j = (_10 * matrix._0) + (_11 * matrix._1) + (_12 * matrix._2);
                float k = (_20 * matrix._0) + (_21 * matrix._1) + (_22 * matrix._2);
                return new Matrix3x1(new float[3, 1] { { i }, { j }, { k } });
            }
            public static Matrix3x3 MultiplyBy3x3(Matrix3x3 first, Matrix3x3 second)
            {
                float[,] a = first.array;
                float[,] b = second.array;
                float[,] output = new float[3, 3];

                //row i
                int rowI = 0;
                for (int ii = 0; ii < 3; ii++) output[rowI, 0] += a[rowI, ii] * b[ii, 0];
                for (int ij = 0; ij < 3; ij++) output[rowI, 1] += a[rowI, ij] * b[ij, 1];
                for (int ik = 0; ik < 3; ik++) output[rowI, 2] += a[rowI, ik] * b[ik, 2];
                //row j
                int rowJ = 1;
                for (int ji = 0; ji < 3; ji++) output[rowJ, 0] += a[rowJ, ji] * b[ji, 0];
                for (int jj = 0; jj < 3; jj++) output[rowJ, 1] += a[rowJ, jj] * b[jj, 1];
                for (int jk = 0; jk < 3; jk++) output[rowJ, 2] += a[rowJ, jk] * b[jk, 2];
                //row k
                int rowK = 2;
                for (int ki = 0; ki < 3; ki++) output[rowK, 0] += a[rowK, ki] * b[ki, 0];
                for (int kj = 0; kj < 3; kj++) output[rowK, 1] += a[rowK, kj] * b[kj, 1];
                for (int kk = 0; kk < 3; kk++) output[rowK, 2] += a[rowK, kk] * b[kk, 2];

                return new Matrix3x3(output);

            }

        }
    }
}
