﻿using MathNet.Spatial.Euclidean;
using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Psi;
using Microsoft.Psi.AzureKinect;
using Microsoft.Psi.Components;

namespace Bodies 
{
    /// <summary>
    /// Simple component that extract the position of a joint from given bodies .
    /// See SimpleBodiesPositionExtractionConfiguration for parameters details.
    /// </summary>
    public class HandsProximityDetector : IConsumerProducer<List<AzureKinectBody>, Dictionary<(uint, uint), List<HandsProximityDetector.HandsProximity>>>
    {
        /// <summary>
        /// Enumerator describing wich hands are close.
        /// </summary>
        public enum HandsProximity { LeftLeft, LeftRight, RightLeft, RightRight };

        /// <summary>
        /// Optionnal reciever that give which pair of bodies to check.
        /// </summary>
        private Connector<List<(uint, uint)>> InPairConnector;
        public Receiver<List<(uint, uint)>> InPair => InPairConnector.In;

        /// <summary>
        /// Reciever of bodies.
        /// </summary>
        private Connector<List<AzureKinectBody>> InConnector;
        public Receiver<List<AzureKinectBody>> In => InConnector.In;

        /// <summary>
        /// Emit if hands are close and send which ones.
        /// </summary>
        public Emitter<Dictionary<(uint, uint), List<HandsProximity>>> Out { get; }

        public HandsProximityDetectorConfiguration Configuration { get; private set; }

        public HandsProximityDetector(Pipeline pipeline, HandsProximityDetectorConfiguration configuration = null)
        {
            Configuration = configuration ?? new HandsProximityDetectorConfiguration();
            InConnector = pipeline.CreateConnector<List<AzureKinectBody>>(nameof(In));
            InPairConnector = pipeline.CreateConnector<List<(uint, uint)>>(nameof(InPair));
            Out = pipeline.CreateEmitter<Dictionary<(uint, uint), List<HandsProximity>>>(this, nameof(Out));
            if (Configuration.IsPairToCheckGiven)
                InConnector.Out.Pair(InPairConnector, DeliveryPolicy.LatestMessage, DeliveryPolicy.LatestMessage).Do(Process);
            else
                InConnector.Out.Do(Process);
        }

        private void Process(List<AzureKinectBody> message, Envelope envelope)
        {
            if (message.Count == 0)
                return;

            Dictionary<(uint, uint), List<HandsProximity>> post = new Dictionary<(uint, uint), List<HandsProximity>>();
            foreach (var body1 in message)
            {
                foreach (var body2 in message)
                {
                    Tuple<(uint, uint), List<HandsProximity>> detected;
                    if (ProcessBodies(body1, body2, out detected))
                        post.Add(detected.Item1, detected.Item2); 
                }
            }
            if (post.Count > 0)
                Out.Post(post, envelope.OriginatingTime);
        }

        private void Process((List<AzureKinectBody>, List<(uint, uint)>) message, Envelope envelope)
        {
            var (bodies, list) = message;
            if (bodies.Count == 0 || list.Count == 0)
                return;
            Dictionary<uint, AzureKinectBody> bodiesDics = new Dictionary<uint,AzureKinectBody>();
            foreach (var body in bodies) 
                bodiesDics.Add(body.TrackingId, body);

            Dictionary<(uint, uint), List<HandsProximity>> post = new Dictionary<(uint, uint), List<HandsProximity>>();
            foreach (var ids in list)
            {
                if(bodiesDics.ContainsKey(ids.Item1) && bodiesDics.ContainsKey(ids.Item2))
                {
                    Tuple<(uint, uint), List<HandsProximity>> detected;
                    if (ProcessBodies(bodiesDics[ids.Item1], bodiesDics[ids.Item2], out detected))
                        post.Add(detected.Item1, detected.Item2);
                }
            }
            if(post.Count > 0)
                Out.Post(post, envelope.OriginatingTime);
        }

        private bool ProcessBodies(in AzureKinectBody bodie1, in AzureKinectBody bodie2, out Tuple<(uint, uint), List<HandsProximity>> detected)
        {
            (Point3D, Point3D) handsB1, handsB2;
            if (!(ProcessHands(bodie1, out handsB1) && ProcessHands(bodie2, out handsB2)))
            {
                detected = new Tuple<(uint, uint), List<HandsProximity>>((bodie1.TrackingId, bodie2.TrackingId), new List<HandsProximity>());
                return false;
            }

            List<HandsProximity> list = new List<HandsProximity>();
            if (ProcessPoints(handsB1.Item1, handsB2.Item2))
                list.Add(HandsProximity.LeftRight);

            if (bodie1.TrackingId != bodie2.TrackingId)
            {
                if (ProcessPoints(handsB1.Item1, handsB2.Item1))
                    list.Add(HandsProximity.LeftLeft);
                if (ProcessPoints(handsB1.Item2, handsB2.Item1))
                    list.Add(HandsProximity.RightLeft);
                if (ProcessPoints(handsB1.Item2, handsB2.Item2))
                    list.Add(HandsProximity.RightRight);
            }
            detected = new Tuple<(uint, uint), List<HandsProximity>>((bodie1.TrackingId, bodie2.TrackingId), list);
            return true;
        }

        private bool ProcessHands(in AzureKinectBody bodie, out (Point3D, Point3D) left_right)
        {    
            var handLeft = bodie.Joints[JointId.HandLeft];
            var handRight = bodie.Joints[JointId.HandRight];
            if (new[] { handLeft, handRight}.Select(j => j.Item2).Any(c => (int)c <= (int)Configuration.MinimumConfidenceLevel))
            {
                left_right = (new Point3D(), new Point3D());
                return false;
            }
            left_right.Item1 = handLeft.Item1.Origin;
            left_right.Item2 = handRight.Item1.Origin;
            return true;
        }

        private bool ProcessPoints(in Point3D origin, in Point3D target)
        {
            return origin.DistanceTo(target) <= Configuration.MinimumDistanceThreshold;
        }
    }
}
