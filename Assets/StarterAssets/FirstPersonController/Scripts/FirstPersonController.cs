using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Header("Crouch Settings")]
		[Tooltip("Move speed while crouching in m/s")]
		public float CrouchSpeed = 2.0f;
		[Tooltip("Height of character controller when crouching")]
		public float CrouchHeight = 1.1f;
		[Tooltip("Camera local Y position when crouching")]
		public float CrouchCameraHeight = 0.65f;
		[Tooltip("Speed of transition between crouching and standing")]
		public float CrouchTransitionSpeed = 10.0f;

		/// <summary>
		/// Whether the character is currently crouching.
		/// Used by EnemyAI to determine if the player is hidden behind cover.
		/// </summary>
		public bool IsCrouching { get; private set; }

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

		// crouch internal state
		private float _standingHeight = 2.0f;
		private Vector3 _standingCenter = new Vector3(0f, 1.0f, 0f);
		private Vector3 _crouchCenter = new Vector3(0f, 0.55f, 0f);
		private float _defaultCameraY = 1.375f;
		private Transform _capsuleMeshTransform;

	
#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
				#else
				return false;
				#endif
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			// cache standing and crouch dimensions
			_standingHeight = _controller.height;
			_standingCenter = _controller.center;
			_crouchCenter = new Vector3(_standingCenter.x, CrouchHeight * 0.5f, _standingCenter.z);

			if (CinemachineCameraTarget != null)
			{
				_defaultCameraY = CinemachineCameraTarget.transform.localPosition.y;
			}

			// find visual body capsule child if present
			Transform capsuleChild = transform.Find("Capsule");
			if (capsuleChild != null)
			{
				_capsuleMeshTransform = capsuleChild;
			}

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

		private void Update()
		{
			Crouch();
			JumpAndGravity();
			GroundedCheck();
			Move();
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		/// <summary>
		/// Uncrouches the player and returns them to full standing height.
		/// </summary>
		public void StandUp()
		{
			IsCrouching = false;
			if (_input != null)
			{
				_input.crouch = false;
			}
		}

		/// <summary>
		/// Crouches the player and lowers their height.
		/// </summary>
		public void CrouchDown()
		{
			IsCrouching = true;
			if (_input != null)
			{
				_input.crouch = false;
			}
		}

		private void Crouch()
		{
			// Check if crouch button was pressed this frame
			bool crouchPressedThisFrame = false;

			if (_input != null && _input.crouch)
			{
				_input.crouch = false; // Immediately consume the trigger!
				crouchPressedThisFrame = true;
			}

#if ENABLE_INPUT_SYSTEM
			if (!crouchPressedThisFrame && Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame)
			{
				crouchPressedThisFrame = true;
			}
#else
			if (!crouchPressedThisFrame && Input.GetKeyDown(KeyCode.LeftControl))
			{
				crouchPressedThisFrame = true;
			}
#endif

			// 1. SPRINT TO UNCROUCH: If crouching and sprint is pressed, immediately stand up!
			if (IsCrouching && _input != null && _input.sprint)
			{
				StandUp();
			}

			// 2. PRESS LEFT CONTROL TO TOGGLE:
			// If crouching -> stand up immediately!
			// If standing -> crouch down!
			if (crouchPressedThisFrame)
			{
				if (IsCrouching)
				{
					StandUp();
				}
				else
				{
					CrouchDown();
				}
			}

			// If crouching, keep sprint disabled so player moves at crouch speed
			if (IsCrouching && _input != null)
			{
				_input.sprint = false;
			}

			// Smoothly interpolate CharacterController height and center
			float targetHeight = IsCrouching ? CrouchHeight : _standingHeight;
			Vector3 targetCenter = IsCrouching ? _crouchCenter : _standingCenter;
			float targetCamY = IsCrouching ? CrouchCameraHeight : _defaultCameraY;

			_controller.height = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * CrouchTransitionSpeed);
			_controller.center = Vector3.Lerp(_controller.center, targetCenter, Time.deltaTime * CrouchTransitionSpeed);

			// Smoothly interpolate camera target Y
			if (CinemachineCameraTarget != null)
			{
				Vector3 camPos = CinemachineCameraTarget.transform.localPosition;
				camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * CrouchTransitionSpeed);
				CinemachineCameraTarget.transform.localPosition = camPos;
			}

			// Smoothly scale the visual body capsule if present
			if (_capsuleMeshTransform != null)
			{
				float heightRatio = _controller.height / _standingHeight;
				Vector3 localScale = _capsuleMeshTransform.localScale;
				localScale.y = Mathf.Lerp(localScale.y, heightRatio, Time.deltaTime * CrouchTransitionSpeed);
				_capsuleMeshTransform.localScale = localScale;

				Vector3 localPos = _capsuleMeshTransform.localPosition;
				localPos.y = Mathf.Lerp(localPos.y, _controller.center.y, Time.deltaTime * CrouchTransitionSpeed);
				_capsuleMeshTransform.localPosition = localPos;
			}
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				
				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

		private void Move()
		{
			// set target speed based on crouch, sprint, or normal walk
			float targetSpeed = MoveSpeed;
			if (IsCrouching)
			{
				targetSpeed = CrouchSpeed;
			}
			else if (_input.sprint)
			{
				targetSpeed = SprintSpeed;
			}

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (_speed < targetSpeed - speedOffset || _speed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(_speed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			// move the player
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// 3. JUMP TO UNCROUCH: If crouching and Jump is pressed, stand up!
				if (_input.jump && IsCrouching)
				{
					_input.jump = false; // Consume jump
					StandUp();
				}
				else if (_input.jump && !IsCrouching && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
}