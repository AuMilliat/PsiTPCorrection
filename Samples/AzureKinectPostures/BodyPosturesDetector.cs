using MathNet.Numerics.RootFinding;
using MathNet.Spatial.Euclidean;
using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Psi;
using Microsoft.Psi.AzureKinect;
using Microsoft.Psi.Components;

namespace Bodies 
{
    public class BodyPosturesDetector : IConsumerProducer<List<AzureKinectBody>, Dictionary<uint, List<BodyPosturesDetector.Posture>>>
    {
        public enum Posture { Standing, Sitting, Pointing_Left, Pointing_Right, ArmCrossed };
        
        private const double DoubleFloatingPointTolerance = double.Epsilon * 2;

        public Receiver<List<AzureKinectBody>> In { get; }

        public Emitter<Dictionary<uint, List<Posture>>> Out { get; }

        private BodyPosturesDetectorConfiguration Configuration;

        public BodyPosturesDetector(Pipeline pipeline, BodyPosturesDetectorConfiguration? configuration = null) 
        {
            Configuration = configuration ?? new BodyPosturesDetectorConfiguration();
            In = pipeline.CreateReceiver<List<AzureKinectBody>>(this, Process, nameof(In));
            Out = pipeline.CreateEmitter<Dictionary<uint, List<BodyPosturesDetector.Posture>>>(this, nameof(Out));
        }

        public void Process(List<AzureKinectBody> bodies, Envelope envelope)
        {
            Dictionary<uint, List<Posture>> postures = new Dictionary<uint, List<Posture>>();
            foreach (var body in bodies) 
            {
               var listing = ProcessBodies(body);
                if(listing.Count > 0)
                    postures.Add(body.TrackingId, listing);
            }
            if (postures.Count > 0)
                Out.Post(postures, envelope.OriginatingTime);
        }

        private List<Posture> ProcessBodies(AzureKinectBody body)
        {
            List<Posture> postures = new List<Posture>();

            if (CheckArmsCrossed(body))
                postures.Add(Posture.ArmCrossed);

            if (CheckPointingLeft(body))
                postures.Add(Posture.Pointing_Left);

            if (CheckPointingRight(body))
                postures.Add(Posture.Pointing_Right);

            var neck = body.Joints[JointId.Neck];
            var pelvis = body.Joints[JointId.Pelvis];
            if (!CheckConfidenceLevel(new[] { neck, pelvis }, Configuration.MinimumConfidenceLevel))
                return postures;

            Line3D reference = new Line3D(pelvis.Pose.Origin, neck.Pose.Origin);

            if (CheckSittings(body, reference))
                postures.Add(Posture.Sitting);

            if (CheckStanding(body, reference))
                postures.Add(Posture.Standing);

            return postures;
        }

        private bool CheckArmsCrossed(in AzureKinectBody body)
        {
            var leftWrist = body.Joints[JointId.WristLeft];
            var leftElbow = body.Joints[JointId.ElbowLeft];
            var rightWrist = body.Joints[JointId.WristRight];
            var rightElbow = body.Joints[JointId.ElbowRight];

            if (!CheckConfidenceLevel(new[] { leftWrist, leftElbow, rightWrist, rightElbow }, Configuration.MinimumConfidenceLevel))
                return false;

            Line3D left = new Line3D(leftWrist.Pose.Origin, leftElbow.Pose.Origin);
            Line3D right = new Line3D(rightWrist.Pose.Origin, rightElbow.Pose.Origin);

            var (pOnLine1, pOnLine2) = left.ClosestPointsBetween(right, mustBeOnSegments: false);
            var (pOnSeg1, pOnSeg2) = left.ClosestPointsBetween(right, mustBeOnSegments: true);
            return pOnLine1.Equals(pOnSeg1, tolerance: Configuration.MinimumDistanceThreshold) && pOnLine2.Equals(pOnSeg2, tolerance: Configuration.MinimumDistanceThreshold);
        }

        private bool CheckSittings(in AzureKinectBody body, in Line3D reference)
        {
            var leftKnee = body.Joints[JointId.KneeLeft];
            var leftHip = body.Joints[JointId.HipLeft];
            var rightKnee = body.Joints[JointId.KneeRight];
            var rightHip = body.Joints[JointId.HipRight];

            if (!CheckConfidenceLevel(new[] { leftKnee, leftHip, rightKnee, rightHip }, Configuration.MinimumConfidenceLevel))
                return false;

            Line3D left = new Line3D(leftKnee.Pose.Origin, leftHip.Pose.Origin);
            Line3D right = new Line3D(rightKnee.Pose.Origin, rightHip.Pose.Origin);
            return AngleToDegrees(reference, left) > Configuration.MinimumSittingDegrees && AngleToDegrees(reference, right) > Configuration.MinimumSittingDegrees;
        }

        private bool CheckStanding(in AzureKinectBody body, in Line3D reference)
        {
            var leftAnkle = body.Joints[JointId.AnkleLeft];
            var leftHip = body.Joints[JointId.HipLeft];
            var rightAnkle = body.Joints[JointId.AnkleRight];
            var rightHip = body.Joints[JointId.HipRight];

            if (!CheckConfidenceLevel(new[] { leftAnkle, leftHip, rightAnkle, rightHip }, Configuration.MinimumConfidenceLevel))
                return false;
     
            Line3D left = new Line3D(leftAnkle.Pose.Origin, leftHip.Pose.Origin);
            Line3D right = new Line3D(rightAnkle.Pose.Origin, rightHip.Pose.Origin);

            return AngleToDegrees(reference, left) < Configuration.MaximumStandingDegrees && AngleToDegrees(reference, right) < Configuration.MaximumStandingDegrees;
        }

        private bool CheckPointingRight(in AzureKinectBody body)
        {
            return CheckPointing(body.Joints[JointId.WristRight], body.Joints[JointId.ElbowRight], body.Joints[JointId.ShoulderRight]);
        }

        private bool CheckPointingLeft(in AzureKinectBody body)
        {
            return CheckPointing(body.Joints[JointId.WristLeft], body.Joints[JointId.ElbowLeft], body.Joints[JointId.ShoulderLeft]);
        }

        private bool CheckPointing(in (MathNet.Spatial.Euclidean.CoordinateSystem, JointConfidenceLevel) wrist, in (MathNet.Spatial.Euclidean.CoordinateSystem, JointConfidenceLevel) elbow, in (MathNet.Spatial.Euclidean.CoordinateSystem, JointConfidenceLevel) shoulder)
        {
            if (!CheckConfidenceLevel(new[] { wrist, elbow, shoulder }, Configuration.MinimumConfidenceLevel))
                return false;

            Line3D Forarm = new Line3D(wrist.Item1.Origin, elbow.Item1.Origin);
            Line3D Arm = new Line3D(wrist.Item1.Origin, shoulder.Item1.Origin);

            return AngleToDegrees(Arm, Forarm) < Configuration.MaximumPointingDegrees;
        }

        private double AngleToDegrees(in Line3D origin, in Line3D target) 
        {
            return origin.Direction.AngleTo(target.Direction).Degrees;
        }

        private bool CheckConfidenceLevel(in (MathNet.Spatial.Euclidean.CoordinateSystem, JointConfidenceLevel)[] array, Microsoft.Azure.Kinect.BodyTracking.JointConfidenceLevel minimumConfidenceLevel)
        {
            if (array.Select(j => j.Item2).Any(c => c <= minimumConfidenceLevel))
                return false;
            return true;
        }
    }
}
