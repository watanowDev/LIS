using Emgu.CV.Dnn;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Model.VisionCam;
using System.Diagnostics;

namespace WATA.LIS.VISION.CAM.Camera
{
    public class V2Detector
    {
        private readonly static string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly static string projectRootDirectory = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(baseDirectory).FullName).FullName).FullName).FullName).FullName;
        private readonly static string modelDirectory = Path.Combine(projectRootDirectory, "Modules", "WATA.LIS.VISION.CAM", "Model");
        private readonly static string DefaultDir = Path.Combine(modelDirectory, "small.onnx");

        private InferenceSession _session;

        public float confThreshold = 0.45f;
        public float iouThreshold = 0.45f;

        public int stride = 32;

        private int maxObjects;
        private int col_len;
        Dictionary<int, int> col_len_caches;

        public V2Detector()
        {
            if (File.Exists(DefaultDir))
            {
                try
                {
                    //var sessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions.MakeSessionOptionWithCudaProvider(0);
                    var sessionOptions = new SessionOptions(); // 기본 옵션은 CPU 실행
                    _session = new InferenceSession(DefaultDir, sessionOptions);

                    maxObjects = _session.OutputMetadata.ElementAt(0).Value.Dimensions[2];
                    col_len = _session.OutputMetadata.ElementAt(0).Value.Dimensions[1];
                    col_len_caches = [];
                    for (int i = 0; i < col_len; i++)
                    {
                        col_len_caches.Add(i, i * maxObjects);
                    }

                }
                catch (Exception ex)
                {
                    //QLog.WriteLine(ex.ToString(), ELog.Error);
                }
            }
            else
            {
                //QLog.WriteLine($"Not exists model : {modelPath}", ELog.Error);
            }
        }

        private Mat PreprocessImage(Mat image, int imageSize, int stride)
        {
            if (image == null || image.Empty())
                throw new ArgumentException("Input image is null or empty");


            // Resize the image to the desired input size
            Mat resizedImage = image.Resize(new OpenCvSharp.Size(imageSize, imageSize));

            // Padding calculation
            int padX = (stride - (resizedImage.Width % stride)) % stride;
            int padY = (stride - (resizedImage.Height % stride)) % stride;

            // Add padding to match stride
            Mat paddedImage = new();
            Cv2.CopyMakeBorder(resizedImage, paddedImage, 0, padY, 0, padX, BorderTypes.Constant, Scalar.All(0));

            return paddedImage;
        }

        public List<V2DetectionModel> Inference(Mat image, float scoreThreshold = 0.5f)
        {
            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                Debug.WriteLine("Inference 시작");

                // 원본 이미지를 복사하여 시각화에 사용
                Mat originalImage = image.Clone();

                // 모델 실행을 위해 BGR2RGB로 변환
                Cv2.CvtColor(image, image, ColorConversionCodes.BGR2RGB);

                Mat resizedImage = PreprocessImage(image, 640, stride);

                // 모델 입력 텐서 생성
                Tensor<float> inputTensor = ConvertInputTensor(resizedImage);

                // ONNX 모델 실행
                using var results = _session.Run(new[]
                {
        NamedOnnxValue.CreateFromTensor("images", inputTensor)
    });

                if (!results.Any())
                    throw new InvalidOperationException("No results returned from the ONNX model inference.");

                var output0 = results.FirstOrDefault(r => r.Name == "output0");
                if (output0 == null)
                    throw new InvalidOperationException("Expected outputs 'output0' or 'output1' were not found in the ONNX model results.");

                Tensor<float> output0Tensor = output0.AsTensor<float>();

                // NonMaxSuppression을 통해 예측 결과(Detection 리스트) 획득
                List<V2DetectionModel> predictions = NonMaxSuppression(output0Tensor, confThreshold, iouThreshold, 100);

                stopwatch.Stop();
                Debug.WriteLine($"Inference 완료 - 총 소요 시간: {stopwatch.ElapsedMilliseconds}ms");

                // 결과 시각화 (원본 이미지를 사용)
                VisualizeDetections(originalImage, predictions, 640, 640);

                return predictions;
            }
            catch
            {
                return new List<V2DetectionModel>();
            }
        }

        public void VisualizeDetections(Mat originalImage, List<V2DetectionModel> detections, int resizedWidth, int resizedHeight)
        {
            // 원본 이미지 크기
            int originalWidth = originalImage.Width;
            int originalHeight = originalImage.Height;

            // 리사이즈 비율 계산
            float scaleX = (float)originalWidth / resizedWidth;
            float scaleY = (float)originalHeight / resizedHeight;

            // ClassId별 색상 매핑
            var classColors = new Dictionary<int, Scalar>
                {
                    { 0, new Scalar(128, 0, 128) },   // 보라색
                    { 1, new Scalar(255, 165, 0) },   // 주황색
                    { 2, new Scalar(0, 0, 255) },     // 빨간색
                    { 3, new Scalar(0, 0, 0) },       // 검은색
                    { 4, new Scalar(255, 255, 255) }, // 흰색
                    { 5, new Scalar(0, 255, 0) },     // 초록색
                    { 6, new Scalar(0, 255, 255) }    // 노란색
                };

            foreach (var detection in detections)
            {
                // 바운딩 박스 좌표 계산 (원본 이미지 크기로 변환)
                float x1 = (detection.CenterX - detection.W / 2) * scaleX;
                float y1 = (detection.CenterY - detection.H / 2) * scaleY;
                float x2 = (detection.CenterX + detection.W / 2) * scaleX;
                float y2 = (detection.CenterY + detection.H / 2) * scaleY;

                // 색상 선택 (ClassId가 매핑되지 않은 경우 기본 색상 사용)
                Scalar color = classColors.ContainsKey(detection.ClassId) ? classColors[detection.ClassId] : new Scalar(255, 255, 255); // 기본 흰색

                // 바운딩 박스 그리기
                Cv2.Rectangle(originalImage, new Point(x1, y1), new Point(x2, y2), color, 2);

                // ClassId와 Confidence 텍스트 추가
                string label = $"Id: {detection.ClassId}, Conf: {detection.Confidence:F2}";
                HersheyFonts fontFace = HersheyFonts.HersheySimplex;
                double fontScale = 0.5;
                int thickness = 1;

                // 텍스트 색상 설정 (ClassId가 3(검은색)일 경우 흰색 텍스트)
                Scalar textColor = detection.ClassId == 3 ? new Scalar(255, 255, 255) : new Scalar(0, 0, 0);

                // 텍스트 배경 박스 크기 계산
                var textSize = Cv2.GetTextSize(label, fontFace, fontScale, thickness, out int baseline);
                var textBoxTopLeft = new Point(x1, y1 - textSize.Height - 5);
                var textBoxBottomRight = new Point(x1 + textSize.Width, y1);

                // 텍스트 배경 박스 그리기
                Cv2.Rectangle(originalImage, textBoxTopLeft, textBoxBottomRight, color, -1);

                // 텍스트 그리기
                Cv2.PutText(originalImage, label, new Point(x1, y1 - 5), fontFace, fontScale, textColor, thickness);
            }

            // 결과 이미지 표시
            Cv2.ImShow("Detections", originalImage);
            Cv2.WaitKey(1); // 키 입력 대기 (1ms)
        }

        private Tensor<float> ConvertInputTensor(Mat image)
        {
            if (image == null || image.Empty())
                throw new ArgumentException("Input image is null or empty");

            int channels = image.Channels();
            int height = image.Rows;
            int width = image.Cols;

            var data = new float[channels * height * width];
            int index = 0;

            for (int c = 0; c < channels; c++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        data[index] = image.At<Vec3b>(y, x)[c] / 255.0f;
                        // if(!(data[index] < 1 && data[index] > 0))
                        // {
                        //     QLog.WriteLine($"data : {data[index]}", ELog.Error);
                        // }
                        index++;
                    }
                }
            }

            // Return data as DenseTensor with shape [1, channels, height, width]
            return new DenseTensor<float>(data, [1, channels, height, width]);
        }

        public static List<V2DetectionModel> NonMaxSuppression(
            Tensor<float> output0Tensor, // Tensor<float>: (1, 37, 8400)
            float confThreshold = 0.25f,
            float iouThreshold = 0.45f,
            int maxDet = 100,
            bool agnostic = false)
        {
            var detections = new List<V2DetectionModel>();

            // Extract dimensions
            int numAnchors = output0Tensor.Dimensions[2]; // 8400
            int numClasses = output0Tensor.Dimensions[1] - 4; // Subtract 4 for bounding box (x, y, w, h)

            for (int anchor = 0; anchor < numAnchors; anchor++)
            {
                float confidence = Math.Max(
                    Math.Max(
                        Math.Max(output0Tensor[0, 4, anchor], output0Tensor[0, 5, anchor]),
                        Math.Max(output0Tensor[0, 6, anchor], output0Tensor[0, 7, anchor])
                    ),
                    Math.Max(
                        Math.Max(output0Tensor[0, 8, anchor], output0Tensor[0, 9, anchor]),
                        output0Tensor[0, 10, anchor]
                    )
                );

                // Object confidence
                if (confidence < confThreshold)
                    continue;

                // Extract bounding box
                float centerX = output0Tensor[0, 0, anchor];
                float centerY = output0Tensor[0, 1, anchor];
                float width = output0Tensor[0, 2, anchor];
                float height = output0Tensor[0, 3, anchor];

                // Convert to (x1, y1, x2, y2)
                float x1 = centerX - width / 2;
                float y1 = centerY - height / 2;
                float x2 = centerX + width / 2;
                float y2 = centerY + height / 2;

                // Find the best class score and ID
                float maxClassScore = 0f;
                int classId = -1;
                for (int c = 0; c < numClasses; c++)
                {
                    float classScore = output0Tensor[0, 4 + c, anchor];
                    classScore = (float)(1.0f / (1.0f + Math.Exp(-classScore))); // Apply Sigmoid
                    if (classScore > maxClassScore)
                    {
                        maxClassScore = classScore;
                        classId = c;
                    }
                    // QLog.WriteLine($"Class: {c}, Negative Class Score: {classScore}");
                }
                // QLog.WriteLine($"{classId} : {maxClassScore}");

                // Combine confidence and class score
                float finalScore = confidence * maxClassScore;
                if (finalScore > confThreshold)
                {

                    detections.Add(new V2DetectionModel
                    {
                        CenterX = centerX,
                        CenterY = centerY,
                        W = width,
                        H = height,
                        Confidence = finalScore,
                        ClassId = classId
                    });
                }
            }

            // Apply Non-Maximum Suppression
            return ApplyNMS(detections, iouThreshold, maxDet, agnostic);
        }

        private static List<V2DetectionModel> ApplyNMS(
            List<V2DetectionModel> detections,
            float iouThres,
            int maxDet,
            bool agnostic)
        {
            var nmsDetections = new List<V2DetectionModel>();

            // Group by class
            var groupedByClass = agnostic
                ? new List<List<V2DetectionModel>> { detections } // Treat all classes as one
                : detections.GroupBy(d => d.ClassId).Select(g => g.ToList()).ToList();

            foreach (var group in groupedByClass)
            {
                var sortedDetections = group.OrderByDescending(d => d.Confidence).ToList();

                while (sortedDetections.Count > 0)
                {
                    var current = sortedDetections[0];
                    nmsDetections.Add(current);
                    sortedDetections.RemoveAt(0);

                    sortedDetections = sortedDetections
                        .Where(d => IoU(current, d) < iouThres)
                        .ToList();

                    if (nmsDetections.Count >= maxDet)
                        break;
                }
            }

            return nmsDetections;
        }

        private static float IoU(V2DetectionModel a, V2DetectionModel b)
        {
            float x1 = Math.Max(a.CenterX - (a.W / 2), b.CenterX - (b.W / 2));
            float y1 = Math.Max(a.CenterY - (a.H / 2), b.CenterY - (b.H / 2));
            float x2 = Math.Min(a.CenterX + (a.W / 2), b.CenterX + (b.W / 2));
            float y2 = Math.Min(a.CenterY + (a.H / 2), b.CenterY + (b.H / 2));

            float intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float areaA = (a.CenterX + (a.W / 2) - (a.CenterX - (a.W / 2))) * (a.CenterY + (a.H / 2) - (a.CenterY - (a.H / 2)));
            float areaB = (b.CenterX + (b.W / 2) - (b.CenterX - (b.W / 2))) * (b.CenterY + (b.H / 2) - (b.CenterY - (b.H / 2)));

            return intersection / (areaA + areaB - intersection);
        }
    }
}
