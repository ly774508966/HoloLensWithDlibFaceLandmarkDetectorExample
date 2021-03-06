﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using OpenCVForUnity.RectangleTrack;
using System.Threading;

#if UNITY_5_3 || UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif
using OpenCVForUnity;
using Rect = OpenCVForUnity.Rect;
using DlibFaceLandmarkDetector;

namespace HoloLensWithDlibFaceLandmarkDetectorExample
{

    /// <summary>
    /// HoloLens AR head example.
    /// </summary>
    [RequireComponent(typeof(OptimizationWebCamTextureToMatHelper))]
    public class HoloLensARHeadExample : MonoBehaviour
    {
        /// <summary>
        /// The enable.
        /// </summary>
        public bool enable = true;

        /// <summary>
        /// The is using separate detection.
        /// </summary>
        public bool isUsingSeparateDetection = false;

        /// <summary>
        /// The using separate detection toggle.
        /// </summary>
        public Toggle isUsingSeparateDetectionToggle;

        /// <summary>
        /// The min detection size ratio.
        /// </summary>
        public float minDetectionSizeRatio = 0.07f;


        [SerializeField, HeaderAttribute ("AR")]

        /// <summary>
        /// The is showing axes.
        /// </summary>
        public bool isShowingAxes;

        /// <summary>
        /// The is showing axes toggle.
        /// </summary>
        public Toggle isShowingAxesToggle;

        /// <summary>
        /// The is showing head.
        /// </summary>
        public bool isShowingHead;

        /// <summary>
        /// The is showing head toggle.
        /// </summary>
        public Toggle isShowingHeadToggle;

        /// <summary>
        /// The is showing effects.
        /// </summary>
        public bool isShowingEffects;

        /// <summary>
        /// The is showing effects toggle.
        /// </summary>
        public Toggle isShowingEffectsToggle;

        /// <summary>
        /// The axes.
        /// </summary>
        public GameObject axes;

        /// <summary>
        /// The head.
        /// </summary>
        public GameObject head;

        /// <summary>
        /// The right eye.
        /// </summary>
        public GameObject rightEye;

        /// <summary>
        /// The left eye.
        /// </summary>
        public GameObject leftEye;

        /// <summary>
        /// The mouth.
        /// </summary>
        public GameObject mouth;

        /// <summary>
        /// The mouth particle system.
        /// </summary>
        ParticleSystem[] mouthParticleSystem;

        /// <summary>
        /// The AR game object.
        /// </summary>
        public GameObject ARGameObject;

        /// <summary>
        /// The AR camera.
        /// </summary>
        public Camera ARCamera;


        /// <summary>
        /// The cam matrix.
        /// </summary>
        Mat camMatrix;

        /// <summary>
        /// The invert Y.
        /// </summary>
        Matrix4x4 invertYM;

        /// <summary>
        /// The transformation m.
        /// </summary>
        Matrix4x4 transformationM;

        /// <summary>
        /// The invert Z.
        /// </summary>
        Matrix4x4 invertZM;

        /// <summary>
        /// The ar m.
        /// </summary>
        Matrix4x4 ARM;


        /// <summary>
        /// The 3d face object points.
        /// </summary>
        MatOfPoint3f objectPoints;

        /// <summary>
        /// The image points.
        /// </summary>
        MatOfPoint2f imagePoints;


        /// <summary>
        /// The rvec.
        /// </summary>
        Mat rvec;

        /// <summary>
        /// The tvec.
        /// </summary>
        Mat tvec;

        /// <summary>
        /// The rot mat.
        /// </summary>
        Mat rotMat;

        /// <summary>
        /// The web cam texture to mat helper.
        /// </summary>
        OptimizationWebCamTextureToMatHelper webCamTextureToMatHelper;

        /// <summary>
        /// The gray mat.
        /// </summary>
        Mat grayMat;

        /// <summary>
        /// The texture.
        /// </summary>
        Texture2D texture;

        /// <summary>
        /// The cascade.
        /// </summary>
        CascadeClassifier cascade;

        /// <summary>
        /// The detection result.
        /// </summary>
        MatOfRect detectionResult;

        /// <summary>
        /// The face landmark detector.
        /// </summary>
        FaceLandmarkDetector faceLandmarkDetector;


        // Camera matrix value of Hololens camera 896x504 size. 
        // These values ​​are unique to my device, obtained from "Windows.Media.Devices.Core.CameraIntrinsics" class. (https://docs.microsoft.com/en-us/uwp/api/windows.media.devices.core.cameraintrinsics)
        // (can adjust the position of the AR hologram with the values ​​of cx and cy. see http://docs.opencv.org/2.4/modules/calib3d/doc/camera_calibration_and_3d_reconstruction.html)
        private double fx = 1035.149;//focal length x.
        private double fy = 1034.633;//focal length y.
        private double cx = 404.9134;//principal point x.
        private double cy = 236.2834;//principal point y.
        private MatOfDouble distCoeffs;
        private double distCoeffs1 = 0.2036923;//radial distortion coefficient k1.
        private double distCoeffs2 = -0.2035773;//radial distortion coefficient k2.
        private double distCoeffs3 = 0.0;//tangential distortion coefficient p1.
        private double distCoeffs4 = 0.0;//tangential distortion coefficient p2.
        private double distCoeffs5 = -0.2388065;//radial distortion coefficient k3.


        private Mat grayMat4Thread;
        private CascadeClassifier cascade4Thread;
        private bool detecting = false;
        private bool didUpdateTheDetectionResult = false;
        private readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();
        private System.Object sync = new System.Object ();

        private bool _isThreadRunning = false;
        private bool isThreadRunning {
            get { lock (sync)
                return _isThreadRunning; }
            set { lock (sync)
                _isThreadRunning = value; }
        }

        private RectangleTracker rectangleTracker;
        private float coeffTrackingWindowSize = 2.0f;
        private float coeffObjectSizeToTrack = 0.85f;
        private Rect[] rectsWhereRegions;
        private List<Rect> detectedObjectsInRegions = new List<Rect> ();
        private List<Rect> resultObjects = new List<Rect> ();

        // Use this for initialization
        void Start ()
        {
            isUsingSeparateDetectionToggle.isOn = isUsingSeparateDetection;

            isShowingAxesToggle.isOn = isShowingAxes;
            isShowingHeadToggle.isOn = isShowingHead;
            isShowingEffectsToggle.isOn = isShowingEffects;

            webCamTextureToMatHelper = gameObject.GetComponent<OptimizationWebCamTextureToMatHelper> ();
            webCamTextureToMatHelper.Init ();

            rectangleTracker = new RectangleTracker ();
            faceLandmarkDetector = new FaceLandmarkDetector (DlibFaceLandmarkDetector.Utils.getFilePath ("shape_predictor_68_face_landmarks.dat"));

            // The coordinates of the detection object on the real world space connected with the pixel coordinates.(mm)
            objectPoints = new MatOfPoint3f (
                new Point3 (-31, 72, 86),//l eye (Interpupillary breadth)
                new Point3 (31, 72, 86),//r eye (Interpupillary breadth)
                new Point3 (0, 40, 114),//nose (Nose top)
                new Point3 (-20, 15, 90),//l mouse (Mouth breadth)
                new Point3 (20, 15, 90),//r mouse (Mouth breadth)
                new Point3 (-69, 76, -2),//l ear (Bitragion breadth)
                new Point3 (69, 76, -2)//r ear (Bitragion breadth)
            );

            imagePoints = new MatOfPoint2f ();
            rvec = new Mat ();
            tvec = new Mat ();
            rotMat = new Mat (3, 3, CvType.CV_64FC1);
        }

        /// <summary>
        /// Raises the web cam texture to mat helper inited event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInited ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperInited");

            Mat webCamTextureMat = webCamTextureToMatHelper.GetDownScaleMat(webCamTextureToMatHelper.GetMat ());

            gameObject.GetComponent<Renderer> ().material.mainTexture = webCamTextureToMatHelper.GetWebCamTexture();

            Debug.Log ("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);

            float width = webCamTextureMat.width();
            float height = webCamTextureMat.height();
            gameObject.transform.localScale = new Vector3 (1, height/width, 1);


            double fx = this.fx;
            double fy = this.fy;
            double cx = this.cx / webCamTextureToMatHelper.DOWNSCALE_RATIO;
            double cy = this.cy / webCamTextureToMatHelper.DOWNSCALE_RATIO;

            camMatrix = new Mat (3, 3, CvType.CV_64FC1);
            camMatrix.put (0, 0, fx);
            camMatrix.put (0, 1, 0);
            camMatrix.put (0, 2, cx);
            camMatrix.put (1, 0, 0);
            camMatrix.put (1, 1, fy);
            camMatrix.put (1, 2, cy);
            camMatrix.put (2, 0, 0);
            camMatrix.put (2, 1, 0);
            camMatrix.put (2, 2, 1.0f);
            Debug.Log ("camMatrix " + camMatrix.dump ());

            distCoeffs = new MatOfDouble (distCoeffs1, distCoeffs2, distCoeffs3, distCoeffs4, distCoeffs5);
            Debug.Log ("distCoeffs " + distCoeffs.dump ());

            //Calibration camera
            Size imageSize = new Size (width, height);
            double apertureWidth = 0;
            double apertureHeight = 0;
            double[] fovx = new double[1];
            double[] fovy = new double[1];
            double[] focalLength = new double[1];
            Point principalPoint = new Point (0, 0);
            double[] aspectratio = new double[1];

            Calib3d.calibrationMatrixValues (camMatrix, imageSize, apertureWidth, apertureHeight, fovx, fovy, focalLength, principalPoint, aspectratio);

            Debug.Log ("imageSize " + imageSize.ToString ());
            Debug.Log ("apertureWidth " + apertureWidth);
            Debug.Log ("apertureHeight " + apertureHeight);
            Debug.Log ("fovx " + fovx [0]);
            Debug.Log ("fovy " + fovy [0]);
            Debug.Log ("focalLength " + focalLength [0]);
            Debug.Log ("principalPoint " + principalPoint.ToString ());
            Debug.Log ("aspectratio " + aspectratio [0]);


            transformationM = new Matrix4x4 ();

            invertYM = Matrix4x4.TRS (Vector3.zero, Quaternion.identity, new Vector3 (1, -1, 1));
            Debug.Log ("invertYM " + invertYM.ToString ());

            invertZM = Matrix4x4.TRS (Vector3.zero, Quaternion.identity, new Vector3 (1, 1, -1));
            Debug.Log ("invertZM " + invertZM.ToString ());


            axes.SetActive (false);
            head.SetActive (false);
            rightEye.SetActive (false);
            leftEye.SetActive (false);
            mouth.SetActive (false);

            mouthParticleSystem = mouth.GetComponentsInChildren<ParticleSystem> (true);


            //If WebCamera is frontFaceing,flip Mat.
            if (webCamTextureToMatHelper.GetWebCamDevice ().isFrontFacing) {
                webCamTextureToMatHelper.flipHorizontal = true;
            }

            grayMat = new Mat (webCamTextureMat.rows (), webCamTextureMat.cols (), CvType.CV_8UC1);
            cascade = new CascadeClassifier ();
            cascade.load (OpenCVForUnity.Utils.getFilePath ("lbpcascade_frontalface.xml"));
//            cascade.load (Utils.getFilePath ("haarcascade_frontalface_alt.xml"));

            // "empty" method is not working on the UWP platform.
            //            if (cascade.empty ()) {
            //                Debug.LogError ("cascade file is not loaded.Please copy from “OpenCVForUnity/StreamingAssets/” to “Assets/StreamingAssets/” folder. ");
            //            }

            grayMat4Thread = new Mat ();
            cascade4Thread = new CascadeClassifier ();
            cascade4Thread.load (OpenCVForUnity.Utils.getFilePath ("haarcascade_frontalface_alt.xml"));

            // "empty" method is not working on the UWP platform.
            //            if (cascade4Thread.empty ()) {
            //                Debug.LogError ("cascade file is not loaded.Please copy from “OpenCVForUnity/StreamingAssets/” to “Assets/StreamingAssets/” folder. ");
            //            }

            detectionResult = new MatOfRect ();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed ()
        {
            Debug.Log ("OnWebCamTextureToMatHelperDisposed");

            StopThread ();

            if (grayMat != null)
                grayMat.Dispose ();

            if (cascade != null)
                cascade.Dispose ();

            if (grayMat4Thread != null)
                grayMat4Thread.Dispose ();

            if (cascade4Thread != null)
                cascade4Thread.Dispose ();

            rectangleTracker.Reset ();

            camMatrix.Dispose ();
            distCoeffs.Dispose ();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode){
            Debug.Log ("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }

        // Update is called once per frame
        void Update ()
        {
            lock (sync) {
                while (ExecuteOnMainThread.Count > 0) {
                    ExecuteOnMainThread.Dequeue ().Invoke ();
                }
            }

            if (webCamTextureToMatHelper.IsPlaying () && webCamTextureToMatHelper.DidUpdateThisFrame ()) {

                Mat rgbaMat = webCamTextureToMatHelper.GetDownScaleMat(webCamTextureToMatHelper.GetMat ());

                Imgproc.cvtColor (rgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);
                Imgproc.equalizeHist (grayMat, grayMat);

                if (enable && !detecting ) {
                    detecting = true;

                    grayMat.copyTo (grayMat4Thread);

                    StartThread (ThreadWorker);
                }

                OpenCVForUnityUtils.SetImage (faceLandmarkDetector, grayMat);

                Rect[] rects;
                if (!isUsingSeparateDetection) {
                    if (didUpdateTheDetectionResult) 
                    {
                        didUpdateTheDetectionResult = false;

                        rectangleTracker.UpdateTrackedObjects (detectionResult.toList());
                    }

                    rectangleTracker.GetObjects (resultObjects, true);

                    rects = rectangleTracker.CreateCorrectionBySpeedOfRects ();

                    if(rects.Length > 0){

                        OpenCVForUnity.Rect rect = rects [0];

                        // Adjust to Dilb's result.
                        rect.y += (int)(rect.height * 0.1f);

                        //detect landmark points
                        List<Vector2> points = faceLandmarkDetector.DetectLandmark (new UnityEngine.Rect (rect.x, rect.y, rect.width, rect.height));

                        UpdateARHeadTransform (points);
                    }

                } else {

                    if (didUpdateTheDetectionResult) {
                        didUpdateTheDetectionResult = false;

                        //Debug.Log("process: get rectsWhereRegions were got from detectionResult");
                        rectsWhereRegions = detectionResult.toArray ();
                    } else {
                        //Debug.Log("process: get rectsWhereRegions from previous positions");
                        rectsWhereRegions = rectangleTracker.CreateCorrectionBySpeedOfRects ();
                    }

                    detectedObjectsInRegions.Clear ();
                    if (rectsWhereRegions.Length > 0) {
                        int len = rectsWhereRegions.Length;
                        for (int i = 0; i < len; i++) {
                            detectInRegion (grayMat, rectsWhereRegions [i], detectedObjectsInRegions);
                        }
                    }

                    rectangleTracker.UpdateTrackedObjects (detectedObjectsInRegions);
                    rectangleTracker.GetObjects (resultObjects, true);

                    if(resultObjects.Count > 0){

                        OpenCVForUnity.Rect rect = resultObjects [0];

                        // Adjust to Dilb's result.
                        rect.y += (int)(rect.height * 0.1f);

                        //detect landmark points
                        List<Vector2> points = faceLandmarkDetector.DetectLandmark (new UnityEngine.Rect (rect.x, rect.y, rect.width, rect.height));

                        UpdateARHeadTransform (points);
                    }
                }
            }
        }


        private void UpdateARHeadTransform(List<Vector2> points)
        {
            // The coordinates in pixels of the object detected on the image projected onto the plane.
            imagePoints.fromArray (
                new Point ((points [38].x + points [41].x) / 2, (points [38].y + points [41].y) / 2),//l eye (Interpupillary breadth)
                new Point ((points [43].x + points [46].x) / 2, (points [43].y + points [46].y) / 2),//r eye (Interpupillary breadth)
                new Point (points [33].x, points [33].y),//nose (Nose top)
                new Point (points [48].x, points [48].y),//l mouth (Mouth breadth)
                new Point (points [54].x, points [54].y) //r mouth (Mouth breadth)
                    ,
                new Point (points [0].x, points [0].y),//l ear (Bitragion breadth)
                new Point (points [16].x, points [16].y)//r ear (Bitragion breadth)
            );

            //Estimate head pose.
            Calib3d.solvePnP (objectPoints, imagePoints, camMatrix, distCoeffs, rvec, tvec);


            if (tvec.get (2, 0) [0] > 0) {

                if (Mathf.Abs ((float)(points [43].y - points [46].y)) > Mathf.Abs ((float)(points [42].x - points [45].x)) / 6.0) {
                    if (isShowingEffects)
                        rightEye.SetActive (true);
                }

                if (Mathf.Abs ((float)(points [38].y - points [41].y)) > Mathf.Abs ((float)(points [39].x - points [36].x)) / 6.0) {
                    if (isShowingEffects)
                        leftEye.SetActive (true);
                }
                if (isShowingHead)
                    head.SetActive (true);
                if (isShowingAxes)
                    axes.SetActive (true);


                float noseDistance = Mathf.Abs ((float)(points [27].y - points [33].y));
                float mouseDistance = Mathf.Abs ((float)(points [62].y - points [66].y));
                if (mouseDistance > noseDistance / 5.0) {
                    if (isShowingEffects) {
                        mouth.SetActive (true);
                        foreach (ParticleSystem ps in mouthParticleSystem) {
                            ps.enableEmission = true;
                            ps.startSize = 40 * (mouseDistance / noseDistance);
                        }
                    }
                } else {
                    if (isShowingEffects) {
                        foreach (ParticleSystem ps in mouthParticleSystem) {
                            ps.enableEmission = false;
                        }
                    }
                }

                Calib3d.Rodrigues (rvec, rotMat);

                transformationM.SetRow (0, new Vector4 ((float)rotMat.get (0, 0) [0], (float)rotMat.get (0, 1) [0], (float)rotMat.get (0, 2) [0], (float)tvec.get (0, 0) [0] / 1000));
                transformationM.SetRow (1, new Vector4 ((float)rotMat.get (1, 0) [0], (float)rotMat.get (1, 1) [0], (float)rotMat.get (1, 2) [0], (float)tvec.get (1, 0) [0] / 1000));
                transformationM.SetRow (2, new Vector4 ((float)rotMat.get (2, 0) [0], (float)rotMat.get (2, 1) [0], (float)rotMat.get (2, 2) [0], (float)tvec.get (2, 0) [0] / 1000 / webCamTextureToMatHelper.DOWNSCALE_RATIO));
                transformationM.SetRow (3, new Vector4 (0, 0, 0, 1));

                // right-handed coordinates system (OpenCV) to left-handed one (Unity)
                ARM = invertYM * transformationM;

                // Apply Z axis inverted matrix.
                ARM = ARM * invertZM;

                // Apply camera transform matrix.
                ARM = ARCamera.transform.localToWorldMatrix * ARM;

                ARUtils.SetTransformFromMatrix (ARGameObject.transform, ref ARM);
            }
        }

        private void StartThread(Action action)
        {
            #if UNITY_METRO && NETFX_CORE
            System.Threading.Tasks.Task.Run(() => action());
            #elif UNITY_METRO
            action.BeginInvoke(ar => action.EndInvoke(ar), null);
            #else
            ThreadPool.QueueUserWorkItem (_ => action());
            #endif
        }

        private void StopThread ()
        {
            if (!isThreadRunning)
                return;

            while (isThreadRunning) {
                //Wait threading stop
            } 
        }

        private void ThreadWorker()
        {
            isThreadRunning = true;

            ObjectDetect ();

            lock (sync) {
                if (ExecuteOnMainThread.Count == 0) {
                    ExecuteOnMainThread.Enqueue (() => {
                        DetectDone ();
                    });
                }
            }

            isThreadRunning = false;
        }

        private void ObjectDetect()
        {
            MatOfRect objects = new MatOfRect ();
            if (cascade4Thread != null)
                cascade4Thread.detectMultiScale (grayMat, objects, 1.1, 2, Objdetect.CASCADE_SCALE_IMAGE, // TODO: objdetect.CV_HAAR_SCALE_IMAGE
                    new Size (grayMat.cols () * minDetectionSizeRatio, grayMat.rows () * minDetectionSizeRatio), new Size ());

            detectionResult = objects;
        }

        private void DetectDone()
        {
            didUpdateTheDetectionResult = true;

            detecting = false;
        }

        private void detectInRegion (Mat img, Rect r, List<Rect> detectedObjectsInRegions)
        {
            Rect r0 = new Rect (new Point (), img.size ());
            Rect r1 = new Rect (r.x, r.y, r.width, r.height);
            Rect.inflate (r1, (int)((r1.width * coeffTrackingWindowSize) - r1.width) / 2,
                (int)((r1.height * coeffTrackingWindowSize) - r1.height) / 2);
            r1 = Rect.intersect (r0, r1);

            if ((r1.width <= 0) || (r1.height <= 0)) {
                Debug.Log ("DetectionBasedTracker::detectInRegion: Empty intersection");
                return;
            }

            int d = Math.Min (r.width, r.height);
            d = (int)Math.Round (d * coeffObjectSizeToTrack);

            MatOfRect tmpobjects = new MatOfRect ();

            Mat img1 = new Mat (img, r1);//subimage for rectangle -- without data copying

            cascade.detectMultiScale (img1, tmpobjects, 1.1, 2, 0 | Objdetect.CASCADE_DO_CANNY_PRUNING | Objdetect.CASCADE_SCALE_IMAGE | Objdetect.CASCADE_FIND_BIGGEST_OBJECT, new Size (d, d), new Size ());


            Rect[] tmpobjectsArray = tmpobjects.toArray ();
            int len = tmpobjectsArray.Length;
            for (int i = 0; i < len; i++) {
                Rect tmp = tmpobjectsArray [i];
                Rect curres = new Rect (new Point (tmp.x + r1.x, tmp.y + r1.y), tmp.size ());
                detectedObjectsInRegions.Add (curres);
            }
        }

        /// <summary>
        /// Raises the disable event.
        /// </summary>
        void OnDisable ()
        {
            webCamTextureToMatHelper.Dispose ();

            if (faceLandmarkDetector != null)
                faceLandmarkDetector.Dispose ();

            if (rectangleTracker != null)
                rectangleTracker.Dispose ();

            if (rvec != null)
                rvec.Dispose ();
            if (tvec != null)
                tvec.Dispose ();
            if (rotMat != null)
                rotMat.Dispose ();
        }

        /// <summary>
        /// Raises the back button event.
        /// </summary>
        public void OnBackButton ()
        {
            #if UNITY_5_3 || UNITY_5_3_OR_NEWER
            SceneManager.LoadScene ("HoloLensWithDlibFaceLandmarkDetectorExample");
            #else
            Application.LoadLevel ("HoloLensWithDlibFaceLandmarkDetectorExample");
            #endif
        }

        /// <summary>
        /// Raises the play button event.
        /// </summary>
        public void OnPlayButton ()
        {
            webCamTextureToMatHelper.Play ();
        }

        /// <summary>
        /// Raises the pause button event.
        /// </summary>
        public void OnPauseButton ()
        {
            webCamTextureToMatHelper.Pause ();
        }

        /// <summary>
        /// Raises the stop button event.
        /// </summary>
        public void OnStopButton ()
        {
            webCamTextureToMatHelper.Stop ();
        }

        /// <summary>
        /// Raises the change camera button event.
        /// </summary>
        public void OnChangeCameraButton ()
        {
            webCamTextureToMatHelper.Init (null, webCamTextureToMatHelper.requestWidth, webCamTextureToMatHelper.requestHeight, !webCamTextureToMatHelper.requestIsFrontFacing);
        }

        /// <summary>
        /// Raises the is using separate detection toggle event.
        /// </summary>
        public void OnIsUsingSeparateDetectionToggle ()
        {
            if (isUsingSeparateDetectionToggle.isOn) {
                isUsingSeparateDetection = true;
            } else {
                isUsingSeparateDetection = false;
            }

            if (rectangleTracker != null)
                rectangleTracker.Reset ();
        }

        /// <summary>
        /// Raises the is showing axes toggle event.
        /// </summary>
        public void OnIsShowingAxesToggle ()
        {
            if (isShowingAxesToggle.isOn) {
                isShowingAxes = true;
            } else {
                isShowingAxes = false;
                axes.SetActive (false);
            }
        }

        /// <summary>
        /// Raises the is showing head toggle event.
        /// </summary>
        public void OnIsShowingHeadToggle ()
        {
            if (isShowingHeadToggle.isOn) {
                isShowingHead = true;
            } else {
                isShowingHead = false;
                head.SetActive (false);
            }
        }

        /// <summary>
        /// Raises the is showin effects toggle event.
        /// </summary>
        public void OnIsShowinEffectsToggle ()
        {
            if (isShowingEffectsToggle.isOn) {
                isShowingEffects = true;
            } else {
                isShowingEffects = false;
                rightEye.SetActive (false);
                leftEye.SetActive (false);
                mouth.SetActive (false);
            }
        }
    }
}