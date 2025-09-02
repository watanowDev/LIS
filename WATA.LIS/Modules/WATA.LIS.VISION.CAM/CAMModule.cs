using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using WATA.LIS.Core;
using Prism.Regions;
using WATA.LIS.VISION.CAM.Views;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.VISION.CAM.Camera;
using OpenCvSharp;
using System.IO;
using System;
using WATA.LIS.VISION.CAM.MQTT;

namespace WATA.LIS.VISION.CAM
{
    public class CAMModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IVisionCamModel _visioncammodel;

        //V2Detector v2detector;

        public CAMModule(IEventAggregator eventAggregator, IVisionCamModel visioncammodel)
        {
            _eventAggregator = eventAggregator;
            _visioncammodel = visioncammodel;

            VisionCamConfigModel visioncam_config = (VisionCamConfigModel)visioncammodel;

            if (visioncam_config.vision_enable == 0)
            {
                //v2detector = new V2Detector();
                //DoV2Detector();
                return;
            }

            if (visioncam_config.vision_name == "HikVision")
            {
                HIKVISION visioncam = new HIKVISION(_eventAggregator, _visioncammodel);
                visioncam.Init();
            }
            else if (visioncam_config.vision_name == "FemtoMega")
            {
                FEMTOMEGA visioncam = new FEMTOMEGA(_eventAggregator, _visioncammodel);
                visioncam.Init();
            }
            else if (visioncam_config.vision_name == "FemtoMega_PCD")
            {
                FEMTOMEGA_PCD visioncam = new FEMTOMEGA_PCD(_eventAggregator, _visioncammodel);
                visioncam.Init();
            }
            else if (visioncam_config.vision_name == "Luxonis")
            {
                Luxonis visioncam = new Luxonis(_eventAggregator, _visioncammodel);
                visioncam.Init();

                DetectionRcv detectionRcv = new DetectionRcv(_eventAggregator);
                DeepImgAnalysis deepImgAnalysis = new DeepImgAnalysis(_eventAggregator);
            }

            //v2detector = new V2Detector();
            //DoV2Detector();
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<CamView>(RegionNames.Content_VisionCam);
        }

        private void DoV2Detector()
        {
            //// NationTest.jpg 파일 절대 경로
            //string imagePath = @"C:\\Users\\USER\\source\\repos\\LIS-ForkLift_mswon\\WATA.LIS\\Modules\\WATA.LIS.VISION.CAM\\Model\\NationTest.jpg";

            //// 파일 존재 여부 확인
            //if (!File.Exists(imagePath))
            //{
            //    throw new FileNotFoundException($"파일을 찾을 수 없습니다: {imagePath}");
            //}

            //// OpenCvSharp Mat 형식으로 이미지 로드
            //Mat image = Cv2.ImRead(imagePath, ImreadModes.Color);

            //// 이미지가 성공적으로 로드되었는지 확인
            //if (image.Empty())
            //{
            //    throw new Exception("이미지를 로드하는 데 실패했습니다.");
            //}

            //// 이후 image 객체를 사용하여 추가 작업 수행 가능

            //v2detector.Inference(image);
        }
    }
}