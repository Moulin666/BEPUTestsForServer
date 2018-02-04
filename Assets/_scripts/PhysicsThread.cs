using UnityEngine;
using BEPUutilities.Threading;
using BEPUphysics.CollisionRuleManagement;
using System.IO;
using System.Xml.Serialization;
using GameCommon.SerializedPhysicsObjects;
using BEPUphysics.Entities.Prefabs;
using System.Collections.Generic;
using BEPUphysics.BroadPhaseEntries;
using BEPUutilities;

public class PhysicsThread : MonoBehaviour
{
	public GameObject PlayerInstance;
	private BEPUphysics.Character.CharacterController CharacterController;
	public BEPUutilities.Vector2 totalMovement;

	public BEPUphysics.Space Space { get; set; }
	private BEPUutilities.Vector3 target;

	private ParallelLooper parallelLooper;
	CollisionGroup characters = new CollisionGroup();

	public float CharacterWeight = 15;

	private RaycastHit hit;

	private void Start()
	{
		parallelLooper = new ParallelLooper();
		parallelLooper.AddThread();

		Space = new BEPUphysics.Space(parallelLooper);

		Space.ForceUpdater.Gravity = new BEPUutilities.Vector3(0, -10, 0);
		Space.TimeStepSettings.TimeStepDuration = 1f / 30f;

		totalMovement = BEPUutilities.Vector2.Zero;
		CharacterController = new BEPUphysics.Character.CharacterController();
		CharacterController.Body.Position = new BEPUutilities.Vector3(PlayerInstance.transform.position.x,
			PlayerInstance.transform.position.y, PlayerInstance.transform.position.z);
		Space.Add(CharacterController);

		//var b = PlayerInstance.GetComponent<BoxCollider>();
		//var center = b.center + b.gameObject.transform.position;

		//BPBox box = new BPBox()
		//{
		//	Center = new PositionData(center.x, center.y, center.z),
		//	HalfExtents = new PositionData(b.size.x / 2f, b.size.y / 2f, b.size.z / 2f),
		//	Rotation = new PositionData(b.transform.rotation.x,
		//		b.transform.rotation.y, b.transform.rotation.z, b.transform.rotation.w),
		//	LocalScale = new PositionData(b.transform.localScale.x, b.transform.localScale.y,
		//		b.transform.localScale.z)
		//};

		//character = new Box(
		//		new BEPUutilities.Vector3(box.Center.X, box.Center.Y, box.Center.Z),
		//		box.LocalScale.X * box.HalfExtents.X * 2,
		//		box.LocalScale.Y * box.HalfExtents.Y * 2,
		//		box.LocalScale.Z * box.HalfExtents.Z * 2, CharacterWeight);

		//character.Orientation = new BEPUutilities.Quaternion(box.Rotation.X, box.Rotation.Y, box.Rotation.Z, box.Rotation.W);

		//Space.Add(character);

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

	public void Update()
	{
		Space.Update();

		if (Input.GetMouseButton(0))
		{
			//RaycastHit hit;
			UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

			if (Physics.Raycast(ray, out hit))
			{
				GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				g.transform.position = hit.point;

				target = new BEPUutilities.Vector3(hit.point.x, hit.point.y, hit.point.z);
			}
		}

		if (BEPUutilities.Vector3.Distance(CharacterController.Body.Position, target) < 1f)
		{
			target = BEPUutilities.Vector3.Zero;
			CharacterController.HorizontalMotionConstraint.MovementDirection = BEPUutilities.Vector2.Zero;
		}

		if (target != BEPUutilities.Vector3.Zero)
		{
			var impulse = new BEPUutilities.Vector3(10, 0, 0);
			var t = target - CharacterController.Body.Position;
			t.Normalize();
			CharacterController.Body.LinearVelocity = t * 3f;
			//CharacterController.Body.(BEPUutilities.Vector3.Normalize(target - CharacterController.Body.Position), impulse);

			//CharacterController.HorizontalMotionConstraint.MovementDirection = BEPUutilities.Vector2.Normalize(
				//new BEPUutilities.Vector2(target.X, target.Z) - 
				//new BEPUutilities.Vector2(CharacterController.Body.Position.X, CharacterController.Body.Position.Z));
		}

		PlayerInstance.transform.position = new UnityEngine.Vector3(
			CharacterController.Body.Position.X,
			CharacterController.Body.Position.Y,
			CharacterController.Body.Position.Z);

		PlayerInstance.transform.rotation = new UnityEngine.Quaternion(
			CharacterController.Body.Orientation.X, CharacterController.Body.Orientation.Y,
			CharacterController.Body.Orientation.Z, CharacterController.Body.Orientation.W);
	}
}
