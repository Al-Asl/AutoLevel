using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(CharacterController), typeof(Animator))]
public class RTSCharacterController : MonoBehaviour
{
    public GameObject markerPrefab;
    [Space]
    public float Speed = 5f;
    public float TurnSpeed = 5f;
    [Space]
    public string idleTrigger = "Idle";
    public string WalkTrigger = "Run"; 

    [HideInInspector]
    public Animator animator;
    private NavMeshAgent navAgent;
    private CharacterController controller;
    private GameObject marker;

    public bool isNavAgentMoving { get; private set; }

    private int idleId;
    private int walkId;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        navAgent.speed = Speed;
        navAgent.angularSpeed = TurnSpeed;

        marker = Instantiate(markerPrefab);
        marker.SetActive(false);

        idleId = Animator.StringToHash(idleTrigger);
        walkId = Animator.StringToHash(WalkTrigger);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if(Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition),out var hit))
            MoveTo(hit.point);
        }

        if (isNavAgentMoving)
        {
            if (!navAgent.pathPending && (navAgent.isPathStale || navAgent.isStopped
                || Mathf.Abs(navAgent.remainingDistance - navAgent.stoppingDistance) <= Speed * Time.deltaTime * 2f))
            {
                StopNavAgent();
            }
        }
    }

    public void MoveTo(Vector3 point)
    {
        navAgent.SetDestination(point);
        marker.SetActive(true);
        marker.transform.position = point;
        animator.ResetTrigger(idleId);
        animator.SetTrigger(walkId);
        isNavAgentMoving = true;
    }

    void StopNavAgent()
    {
        if (navAgent.enabled)
            navAgent.ResetPath();
        marker.SetActive(false);
        animator.ResetTrigger(walkId);
        animator.SetTrigger(idleId);
        isNavAgentMoving = false;
    }
}