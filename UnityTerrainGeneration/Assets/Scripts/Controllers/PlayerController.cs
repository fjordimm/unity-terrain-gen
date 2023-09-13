using UnityEngine;

namespace UnityTerrainGeneration.Controllers
{
	public class PlayerController : MonoBehaviour
	{
		[SerializeField] private Transform PlayerCam;
		[SerializeField] private GameObject PlayerBody;

		[SerializeField] private float CamSensitivityX;
		[SerializeField] private float CamSensitivityY;

		private float camRotationX;
		private float camRotationY;

		[SerializeField] private float MovementSpeed;

		private Rigidbody playerRigidbody;

		[SerializeField] private LayerMask GroundLayerMask;
		[SerializeField] private float GroundDrag;
		[SerializeField] private float AirDrag;
		[SerializeField] private float GroundRaycastExtraDist;

		private void Start()
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;

			playerRigidbody = this.GetComponent<Rigidbody>();
			playerRigidbody.freezeRotation = true;
		}

		private void Update()
		{
			camRotationX -= Input.GetAxisRaw("Mouse Y") * CamSensitivityX;
			camRotationX = Mathf.Clamp(camRotationX, -90f, 90f);

			camRotationY += Input.GetAxisRaw("Mouse X") * CamSensitivityY;

			this.transform.rotation = Quaternion.Euler(0f, camRotationY, 0f);
			PlayerCam.transform.rotation = Quaternion.Euler(camRotationX, camRotationY, 0f);

			///////
			
			float verticalMovement = Input.GetAxisRaw("Vertical");
			float horizontalMovement = Input.GetAxisRaw("Horizontal");

			Vector3 direction = (this.transform.forward * verticalMovement + this.transform.right * horizontalMovement).normalized;

			playerRigidbody.AddForce(direction * MovementSpeed * Time.deltaTime, ForceMode.Force);

			///////
			
			bool isOnGround = Physics.Raycast(
				PlayerBody.transform.position,
				Vector3.down,
				PlayerBody.GetComponent<CapsuleCollider>().height / 2f + GroundRaycastExtraDist,
				GroundLayerMask
			);

			if (isOnGround)
			{ playerRigidbody.drag = GroundDrag; }
			else
			{ playerRigidbody.drag = AirDrag; }

			if (isOnGround)
			{ playerRigidbody.AddForce(Vector3.up * direction.magnitude * Time.deltaTime * 5000f, ForceMode.Force); }
		}
	}
}
