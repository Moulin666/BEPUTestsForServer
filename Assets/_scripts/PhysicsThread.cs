using UnityEngine;
using BEPUutilities.Threading;
using BEPUphysics.CollisionRuleManagement;
using System.IO;
using System.Xml.Serialization;
using GameCommon.SerializedPhysicsObjects;
using BEPUphysics.Entities.Prefabs;
using System.Collections.Generic;
using BEPUphysics.BroadPhaseEntries;
using BEPUphysics.Paths.PathFollowing;
using BEPUphysics.Paths;
using BEPUphysics.Entities;
using BEPUutilities;

public class PhysicsThread : MonoBehaviour
{
	public GameObject pl;

	private EntityMover mover;
	private Path<BEPUutilities.Quaternion> orientationPath;
	private Path<BEPUutilities.Vector3> positionPath;
	private EntityRotator rotator;
	private double pathTime;

	public BEPUphysics.Space Space { get; set; }

	private ParallelLooper parallelLooper;
	CollisionGroup characters = new CollisionGroup();

	//public float characterHeight = 1.75f;
	//public float characterWidth = 0.75f;

	private void Start()
	{
		parallelLooper = new ParallelLooper();
		parallelLooper.AddThread();
		parallelLooper.AddThread();
		parallelLooper.AddThread();
		parallelLooper.AddThread();

		Space = new BEPUphysics.Space(parallelLooper);

		Space.ForceUpdater.Gravity = new BEPUutilities.Vector3(0, -10f, 0);
		Space.TimeStepSettings.TimeStepDuration = 1f / 30f;

		setplayer();

		var groupPair = new CollisionGroupPair(characters, characters);
		CollisionRules.CollisionGroupRules.Add(groupPair, CollisionRule.NoBroadPhase);

		string FilePath = Application.dataPath + "/Resources/Physics.xml";

		FileStream f = File.OpenRead(FilePath);
		XmlSerializer serializer = new XmlSerializer(typeof(BPColliders));
		BPColliders colliders = (BPColliders)serializer.Deserialize(f);

		// Box colliders
		foreach (var bpBox in colliders.Boxes)
		{
			var groundShape = new Box(
				new BEPUutilities.Vector3(bpBox.Center.X, bpBox.Center.Y, bpBox.Center.Z),
				bpBox.LocalScale.X * bpBox.HalfExtents.X * 2,
				bpBox.LocalScale.Y * bpBox.HalfExtents.Y * 2,
				bpBox.LocalScale.Z * bpBox.HalfExtents.Z * 2);

			groundShape.Orientation = new BEPUutilities.Quaternion(bpBox.Rotation.X, bpBox.Rotation.Y, bpBox.Rotation.Z, bpBox.Rotation.W);

			Space.Add(groundShape);
		}

		// Capsule colliders
		foreach (var bpCapsule in colliders.Capsules)
		{
			var groundShape = new Capsule(
				new BEPUutilities.Vector3(bpCapsule.Center.X, bpCapsule.Center.Y, bpCapsule.Center.Z),
				bpCapsule.LocalScale.Y * bpCapsule.Height, bpCapsule.LocalScale.Z * bpCapsule.Radius);

			groundShape.Orientation = new BEPUutilities.Quaternion(bpCapsule.Rotation.X, bpCapsule.Rotation.Y, bpCapsule.Rotation.Z, bpCapsule.Rotation.W);

			Space.Add(groundShape);
		}

		// Sphere colliders
		foreach (var bpSphere in colliders.Spheres)
		{
			var groundShape = new Sphere(
				new BEPUutilities.Vector3(bpSphere.Center.X, bpSphere.Center.Y, bpSphere.Center.Z),
				bpSphere.LocalScale.X * bpSphere.Radius);

			groundShape.Orientation = new BEPUutilities.Quaternion(bpSphere.Rotation.X, bpSphere.Rotation.Y, bpSphere.Rotation.Z, bpSphere.Rotation.W);

			Space.Add(groundShape);
		}

		// Mesh colliders
		foreach (var bpMesh in colliders.Meshes)
		{
			List<BEPUutilities.Vector3> vList = new List<BEPUutilities.Vector3>();

			foreach(var data in bpMesh.Vertexes)
			{
				vList.Add(new BEPUutilities.Vector3(data.X, data.Y, data.Z));
			}

			StaticMesh groundShape = new StaticMesh(vList.ToArray(), bpMesh.Triangles.ToArray(),
				new AffineTransform(new BEPUutilities.Vector3(bpMesh.LocalScale.X, bpMesh.LocalScale.Y, bpMesh.LocalScale.Z),
				new BEPUutilities.Quaternion(bpMesh.Rotation.X, bpMesh.Rotation.Y, bpMesh.Rotation.Z, bpMesh.Rotation.W),
				new BEPUutilities.Vector3(bpMesh.Center.X, bpMesh.Center.Y, bpMesh.Center.Z)));

			Space.Add(groundShape);
		}

		f.Close();
	}

	public void setplayer()
	{
		Entity movingEntity;

		movingEntity = new Capsule(new BEPUutilities.Vector3(40, 0f, 0), 5, 5);
		movingEntity.BecomeDynamic(10);

		/*var slerpCurve = new QuaternionSlerpCurve();
		slerpCurve.ControlPoints.Add(0, BEPUutilities.Quaternion.Identity);

		slerpCurve.PostLoop = CurveEndpointBehavior.Clamp;
		orientationPath = slerpCurve;*/
	
		mover = new EntityMover(movingEntity);

		rotator = new EntityRotator(movingEntity);

		mover.LinearMotor.Settings.Servo.SpringSettings.Stiffness /= 1000;
		mover.LinearMotor.Settings.Servo.SpringSettings.Damping /= 1000;

		Space.Add(movingEntity);
		Space.Add(mover);
		Space.Add(rotator);
	}

	public void Update()
	{
		Space.Update();

		if (Input.GetMouseButton(0))
		{
			RaycastHit hit;
			UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

			if (Physics.Raycast(ray, out hit))
			{
				MoveToPosition(pl.transform.position, hit.point);
			}
		}

		if (positionPath != null)
		{
			pathTime += Space.TimeStepSettings.TimeStepDuration;
			mover.TargetPosition = positionPath.Evaluate(pathTime);
			//rotator.TargetOrientation = orientationPath.Evaluate(pathTime);

			pl.transform.position = new UnityEngine.Vector3(mover.Entity.Position.X, mover.Entity.Position.Y, mover.Entity.Position.Z);
			//pl.transform.rotation = new UnityEngine.Quaternion(rotator.Entity.Orientation.X, rotator.Entity.Orientation.Y,
				//rotator.Entity.Orientation.Z, rotator.Entity.Orientation.W);
		}
	}

	public void MoveToPosition(UnityEngine.Vector3 startPoint, UnityEngine.Vector3 endPoint)
	{
		pathTime = 0;
		positionPath = null;

		var wrappedPositionCurve = new LinearInterpolationCurve3D
		{
			PreLoop = CurveEndpointBehavior.Clamp,
			PostLoop = CurveEndpointBehavior.Clamp
		};

		wrappedPositionCurve.ControlPoints.Add(0, new BEPUutilities.Vector3(startPoint.x, startPoint.y, startPoint.z));
		wrappedPositionCurve.ControlPoints.Add(1, new BEPUutilities.Vector3(endPoint.x, endPoint.y, endPoint.z));

		// TO DO Rotator

		positionPath = new ConstantLinearSpeedCurve(5, wrappedPositionCurve);
	}
}
