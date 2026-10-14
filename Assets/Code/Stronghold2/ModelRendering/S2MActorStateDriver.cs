using Assets.Code.Stronghold2.AssetLoading;
using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>
  /// The smallest honest state DRIVER: it moves an actor through the world and
  /// switches the actor's clip to match.
  ///
  /// WHAT WAS MISSING: the per-state clip table (<see cref="ActorStateTable"/>)
  /// says WHICH clip a type plays in each state, but nothing in the game ever
  /// set a state - a .s2m stores every actor at rest, so an actor only switched
  /// clip when a harness called <see cref="S2MActorAnimation.AttachState"/>
  /// directly. This component is the thing that would: give it a destination and
  /// it walks there, playing the clip its own catalogue row names, then goes
  /// back to idle.
  ///
  /// THREE DELIBERATE CHOICES:
  ///
  ///  1. THE CLIP IS NOT THE MOVEMENT. Measured over the whole catalogue the
  ///     walk/run clips are IN-PLACE cycles - the skeleton root's net drift per
  ///     loop is 0.000..0.05 m (<see cref="S2MActorClipMetrics.RootDriftPerLoop"/>),
  ///     exactly like the idles - so a clip carries no root motion and there is
  ///     nothing to read a speed off. The translation is this component's job;
  ///     <see cref="Speed"/> is a caller constant, documented as unverified
  ///     because the install ships no unit-speed table (the same reason the
  ///     +75/+92 header constants still have no names).
  ///
  ///  2. A STATE CHANGE RE-CLICKS ONCE. <see cref="S2MActorAnimation.AttachState"/>
  ///     rebuilds an AnimationClip and re-samples from it, so it is called only
  ///     when the state actually changes (<see cref="AttachCount"/>), never per
  ///     frame. The switch has no blend - the legacy Animation component has no
  ///     blend tree and no cross-fade data exists - which is a cut, as the game's
  ///     own flat transform tracks play.
  ///
  ///  3. A TYPE WITH NO CLIP FOR A STATE STILL MOVES. The state is set anyway
  ///     (the actor IS walking, whether or not a clip says so) and the previous
  ///     clip is kept, with the reason recorded in <see cref="LastReason"/> -
  ///     the same rule the placement loader already follows. That is why the
  ///     archer, whose own list names no WALK, can be driven and reports
  ///     "Archer has no walk clip in its catalogue".
  ///
  /// <see cref="Tick"/> is a plain step function so a batchmode harness can drive
  /// the actor on a fixed timestep with no Play mode; <see cref="Update"/> just
  /// feeds it the real frame delta for in-engine use.
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class S2MActorStateDriver : MonoBehaviour
  {
    /// <summary>
    /// Metres per second the driver walks. A PLACEHOLDER, not a decoded value:
    /// no shipped table gives a troop's speed, and the clips carry none (choice 1
    /// above), so this is the caller's number until the exe phase names one.
    /// </summary>
    public const float DefaultSpeed = 1.4f;

    /// <summary>The actor's catalogue type (the entity JSON's "type"), which is
    /// what the state table is keyed by.</summary>
    public string ActorType;

    /// <summary>Game install root the ".osh2anim" caches are resolved against.</summary>
    public string GameInstallPath;

    /// <summary><see cref="DefaultSpeed"/> unless a caller says otherwise.</summary>
    public float Speed = DefaultSpeed;

    /// <summary>How close counts as arrived, in metres. One frame of travel at
    /// the default speed is ~0.023 m, so this stops an actor from jittering on
    /// the last fraction of a step.</summary>
    public float ArriveEpsilon = 0.02f;

    /// <summary>The state the driver last asked for. Set even when the type has
    /// no clip for it - see choice 3.</summary>
    public ActorState State { get; private set; } = ActorState.Idle;

    /// <summary>Whether a destination is outstanding.</summary>
    public bool IsMoving { get; private set; }

    /// <summary>The outstanding destination, in world space.</summary>
    public Vector3 Destination { get; private set; }

    /// <summary>Frames since the state last changed, including the frame it
    /// changed on.</summary>
    public int FramesInState { get; private set; } = 1;

    /// <summary>Frames this driver has stepped.</summary>
    public int TotalFrames { get; private set; }

    /// <summary>How many times the clip was (re)built for a state change. Must
    /// equal the number of transitions, or the driver is re-clipping per
    /// frame.</summary>
    public int AttachCount { get; private set; }

    /// <summary>How many times <see cref="State"/> changed.</summary>
    public int TransitionCount { get; private set; }

    /// <summary>The clip the actor ended up playing (game-relative .gr2 path),
    /// or null when the actor has never had a clip.</summary>
    public string ClipPath { get; private set; }

    /// <summary>Why the last state change did NOT switch clip, or null when it
    /// did. A type gap (no row for the state) and a failed bind both land
    /// here.</summary>
    public string LastReason { get; private set; }

    /// <summary>World distance walked since this driver was started.</summary>
    public float DistanceWalked { get; private set; }

    /// <summary>Set by <see cref="Stop"/>, consumed by the first <see cref="Tick"/>
    /// after it: the halt owes the state one Idle transition and this is how the
    /// tick knows the difference between a halt and a state that never was a
    /// destination.</summary>
    private bool haltPending;

    /// <summary>
    /// Attaches the clip for <paramref name="state"/> once and adopts it as the
    /// current state, without moving anything. Use for a state that is not a
    /// destination (an attack) or to force the initial idle.
    /// </summary>
    public void Begin(ActorState state)
    {
      if (state != State)
      {
        State = state;
        TransitionCount++;
      }
      FramesInState = 1;
      Attach(state);
    }

    /// <summary>
    /// Walks to <paramref name="destination"/>: the actor is put in
    /// <see cref="ActorState.Walk"/> on the next tick and returns to
    /// <see cref="ActorState.Idle"/> when it arrives.
    /// </summary>
    public void WalkTo(Vector3 destination)
    {
      Destination = destination;
      IsMoving = true;
      // A new order supersedes a halt that has not been ticked yet: the actor
      // is walking again, so there is no landing left to apply.
      haltPending = false;
    }

    /// <summary>Stops where it is; the state drops back to Idle on the next
    /// tick, which is the transition <see cref="Tick"/> applies for a halt
    /// exactly as it does for an arrival.</summary>
    public void Stop()
    {
      IsMoving = false;
      Destination = transform.position;
      haltPending = true;
    }

    /// <summary>
    /// One deterministic step of <paramref name="deltaTime"/> seconds: move
    /// toward the destination, then make the state match (walking while there is
    /// distance left, idle on arrival, idle one tick after a
    /// <see cref="Stop"/>).
    ///
    /// The state still matches when there is nothing to move: a halt requested
    /// between two ticks arrives here as "not moving", and the Idle transition
    /// that <see cref="Stop"/> documents is applied HERE, once. Without it the
    /// early return below kept a stopped actor in <see cref="ActorState.Walk"/>
    /// forever - standing still, playing its walk clip, with
    /// <see cref="TransitionCount"/> frozen at the walk transition. Measured in
    /// batchmode before the fix: 5 checks failed ("every stopped actor has
    /// landed in Idle one tick later (0 of 4)").
    ///
    /// A state that is NOT a destination is left alone: only a
    /// <see cref="Stop"/> sets <see cref="haltPending"/>, so an attack state
    /// adopted through <see cref="Begin"/> survives an idle tick instead of
    /// being reset to Idle.
    /// </summary>
    public void Tick(float deltaTime)
    {
      TotalFrames++;
      FramesInState++;
      if (!IsMoving)
      {
        if (haltPending)
        {
          haltPending = false;
          if (State != ActorState.Idle) SetState(ActorState.Idle);
        }
        return;
      }
      if (deltaTime <= 0f) return;

      Vector3 position = transform.position;
      Vector3 to = Destination - position;
      to.y = 0f;
      float remaining = to.magnitude;
      if (remaining <= ArriveEpsilon)
      {
        transform.position = new Vector3(Destination.x, position.y, Destination.z);
        IsMoving = false;
        if (State != ActorState.Idle) SetState(ActorState.Idle);
        return;
      }
      // y is left alone: the terrain surface is the renderer's business, and a
      // destination is a ground point only in x/z.
      Vector3 step = to / remaining * Mathf.Min(Speed * deltaTime, remaining);
      transform.position = position + step;
      DistanceWalked += step.magnitude;
      if (State != ActorState.Walk) SetState(ActorState.Walk);
    }

    private void Update()
    {
      Tick(Time.deltaTime);
    }

    /// <summary>
    /// Changes the state and switches the clip. The clip lookup failure is NOT a
    /// failure of the move: the caller learns about it through
    /// <see cref="LastReason"/> and the actor keeps the clip it had.
    /// </summary>
    private void SetState(ActorState state)
    {
      State = state;
      TransitionCount++;
      FramesInState = 1;
      Attach(state);
    }

    private void Attach(ActorState state)
    {
      if (string.IsNullOrEmpty(ActorType) || string.IsNullOrEmpty(GameInstallPath))
      {
        LastReason = $"no type ({ActorType ?? "null"}) or game root to look a {state} clip up with";
        return;
      }
      if (!ActorStateTable.TryLoad(ActorType, state, GameInstallPath, out Granny2AnimData anim,
        out string reason))
      {
        // A type gap is shipped data (the archer's own list names no WALK), not a
        // decode failure: keep the clip the actor has and say why.
        LastReason = reason;
        return;
      }
      try
      {
        S2MActorAnimation.Attached attached = S2MActorAnimation.AttachState(gameObject, anim);
        if (attached.Animation == null)
        {
          LastReason = $"the {state} clip '{anim.ClipName}' bound no track on this skeleton";
          return;
        }
        ClipPath = attached.ClipPath;
        AttachCount++;
        LastReason = null;
      }
      catch (Exception exception)
      {
        // One bad clip must not throw the driver's step, exactly as the
        // placement loader wraps its attach.
        LastReason = $"{exception.GetType().Name}: {exception.Message}";
      }
    }

    /// <summary>One line for a report: state, clip, and why it might not have one.</summary>
    public string Describe() =>
      $"{ActorType} {State}"
      + (ClipPath == null ? " (no clip)" : $" -> {System.IO.Path.GetFileNameWithoutExtension(ClipPath)}")
      + (LastReason == null ? "" : $" [{LastReason}]");
  }
}
