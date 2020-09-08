﻿using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Mirror;
using UnityEngine;
using static GroundCheck;

public class Player : NetworkBehaviour {
	[SerializeField] float movementSpeed = 7f;
	[SerializeField] float jumpSpeed = 7f;
	[SerializeField] int maxJumps = 2;
	[SerializeField] int jumpsAvailable;
	[SerializeField] float wallslideTriggerSpeed = 0.1f; //upward direction
	[SerializeField] float wallslideSpeed = -0.1f; //downward direction
	
	// GameObject instead of particle system to pass into a server command
	[SerializeField] private GameObject dustParticles;
	
	// Ground and wall checks are preformed with the following gameobjects.
	[SerializeField] private GameObject groundCheck;
	private GroundCheck groundCheckScript;
	[SerializeField] private GameObject LeftWallCheck;
	private WallCheck leftWallCheckScript;
	[SerializeField] private GameObject RightWallCheck;
	private WallCheck rightWallCheckScript;

	private Rigidbody2D rb;
	private BoxCollider2D boxCollider;

	// Start is called before the first frame update
	void Start() {
		rb = GetComponent<Rigidbody2D>();
		boxCollider = GetComponent<BoxCollider2D>();
		jumpsAvailable = maxJumps;

		//load scripts from groundCheck and LeftWallCheck gameobjects
		groundCheckScript = groundCheck.GetComponent<GroundCheck>();
		leftWallCheckScript = LeftWallCheck.GetComponent<WallCheck>();
		rightWallCheckScript = RightWallCheck.GetComponent<WallCheck>();

		ConnectClientToCamera();
		DisablePhysicsIfOtherPlayer();
	}

	private void ConnectClientToCamera() {
		// Ensure the player belongs to this client
		if (!hasAuthority) { return; }

		CinemachineStateDrivenCamera camera = FindObjectOfType<CinemachineStateDrivenCamera>();
		camera.Follow = transform;
		camera.LookAt = transform;
	}

	private void DisablePhysicsIfOtherPlayer() {
		// Kinematic rigidbodys do not undergo physics calculations
		// This disables physics if the player belongs to a different client
		// Doing this will remove jittery players but also remove basic player - player collision
		rb.isKinematic = !hasAuthority;
	}

	// Update is called once per frame
	void Update() {
		if (hasAuthority) {
			Debug.Log(GetJumpState());
			// the order of these operations is important due to them changing the "JumpState" of the player.
			handleLanding();
			handleJump();
			handleWallslide();
			handleMovement();
		}
	}

	private void handleLanding() {
		if (GetJumpState() == GroundCheck.JumpState.LANDED) {
			// As soon as landing has been detected, it should perform the following lines once
			// and then immediately change the JumpState to ONGROUND, so these line won't run again.
			jumpsAvailable = maxJumps;
			CmdPlayDustParticles();
			SetJumpState(GroundCheck.JumpState.ONGROUND);
		}
	}

	private void handleMovement() {
		Vector2 movementVector = new Vector2(Input.GetAxisRaw("Horizontal") * movementSpeed, rb.velocity.y);
		rb.velocity = movementVector;
	}

	private void handleJump() {
		if (jumpsAvailable <= 0) {
			return;
		}
		if (Input.GetButtonDown("Jump")) {
			Vector2 jumpVelocity = new Vector2(rb.velocity.x, jumpSpeed);
			rb.velocity = jumpVelocity;
			jumpsAvailable--;
			// The following lines ensure that when the player falls off the edge and tries to jump, his available jumps decrements twice.
			if (GetJumpState() == GroundCheck.JumpState.FALLING) {
				jumpsAvailable--;
				SetJumpState(GroundCheck.JumpState.INAIR);
			}
		}
	}

	private void handleWallslide() {
		// if player is moving left onto a left wall or moving right onto a right wall, wall sliding will activate.
		if ((GetIsOnLeftWall() && Input.GetAxisRaw("Horizontal") < 0) ||
			(GetIsOnRightWall() && Input.GetAxisRaw("Horizontal") > 0)) {
			if (rb.velocity.y < wallslideTriggerSpeed) {
				SetJumpState(GroundCheck.JumpState.LANDED);
				CmdPlayDustParticles();
				if (rb.velocity.y < wallslideSpeed) {
					Vector2 slideVelocity = new Vector2(rb.velocity.x, wallslideSpeed);
					rb.velocity = slideVelocity;
				}
			}
		}
	}

	// Run on server so every player can see the dust particles
	[Command]
	private void CmdPlayDustParticles() {
		ClientPlayDustParticles();
	}

	[ClientRpc]
	private void ClientPlayDustParticles() {
		dustParticles.GetComponent<ParticleSystem>().Play();
	}

	// checks if the small groundcheck collision box below player is triggered.
	private JumpState GetJumpState() {
		return groundCheckScript.GetJumpState();
	}

	private void SetJumpState(JumpState js) {
		groundCheckScript.SetJumpState(js);
	}

	private bool GetIsOnLeftWall() {
		return leftWallCheckScript.GetIsOnWall();
	}

	private bool GetIsOnRightWall() {
		return rightWallCheckScript.GetIsOnWall();
	}

}