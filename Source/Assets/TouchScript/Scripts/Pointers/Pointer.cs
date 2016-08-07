/*
 * @author Valentin Simonov / http://va.lent.in/
 */

using System;
using System.Text;
using TouchScript.Hit;
using TouchScript.InputSources;
using TouchScript.Layers;
using TouchScript.Utils;
using UnityEngine;

namespace TouchScript.Pointers
{
    /// <summary>
    /// <para>Representation of a pointer (touch, mouse) within TouchScript.</para>
    /// <para>An instance of this class is created when user touches the screen. A unique id is assigned to it which doesn't change throughout its life.</para>
    /// <para><b>Attention!</b> Do not store references to these objects beyond pointer's lifetime (i.e. when target finger is lifted off). These objects may be reused internally. Store unique ids instead.</para>
    /// </summary>
    public class Pointer : IPointer
    {
        #region Constants

        /// <summary>
        /// Invalid pointer id.
        /// </summary>
        public const int INVALID_POINTER = -1;

        /// <summary>
        /// Indicates that this pointer is generated by script and is not mapped to any device input.
        /// </summary>
        public const uint FLAG_ARTIFICIAL = 1 << 0;

        public const uint FLAG_RETURNED = 1 << 1;

        /// <summary>
        /// Pointer type.
        /// </summary>
        public enum PointerType
        {
            /// <summary>
            /// Unknown.
            /// </summary>
            Unknown,

            /// <summary>
            /// Touch.
            /// </summary>
            Touch,

            /// <summary>
            /// Mouse.
            /// </summary>
            Mouse,

            /// <summary>
            /// Pen.
            /// </summary>
            Pen,

            /// <summary>
            /// Object.
            /// </summary>
            Object
        }

        [Flags]
        public enum PointerButtonState
        {
            Nothing = 0,

            /// <summary>
            /// Indicates a primary action, analogous to a left mouse button down.
            /// A <see cref="TouchPointer"/> or <see cref="ObjectPointer"/> has this flag set when it is in contact with the digitizer surface.
            /// A <see cref="PenPointer"/> has this flag set when it is in contact with the digitizer surface with no buttons pressed.
            /// A <see cref="MousePointer"/> has this flag set when the left mouse button is down.
            /// </summary>
            FirstButtonPressed = 1 << 0,

            /// <summary>
            /// First button pressed this frame.
            /// </summary>
            FirstButtonDown = 1 << 1,

            /// <summary>
            /// First button released this frame.
            /// </summary>
            FirstButtonUp = 1 << 2,

            /// <summary>
            /// Indicates a secondary action, analogous to a right mouse button down.
            /// A <see cref="TouchPointer"/> or <see cref="ObjectPointer"/> does not use this flag.
            /// A <see cref="PenPointer"/> has this flag set when it is in contact with the digitizer surface with the pen barrel button pressed.
            /// A <see cref="MousePointer"/> has this flag set when the right mouse button is down.
            /// </summary>
            SecondButtonPressed = 1 << 3,

            /// <summary>
            /// Second button pressed this frame.
            /// </summary>
            SecondButtonDown = 1 << 4,

            /// <summary>
            /// Second button released this frame.
            /// </summary>
            SecondButtonUp = 1 << 5,

            /// <summary>
            /// Analogous to a mouse wheel button down.
            /// A <see cref="TouchPointer"/>, <see cref="PenPointer"/> or <see cref="ObjectPointer"/> does not use this flag.
            /// A <see cref="MousePointer"/> has this flag set when the mouse wheel button is down.
            /// </summary>
            ThirdButtonPressed = 1 << 6,

            /// <summary>
            /// Third button pressed this frame.
            /// </summary>
            ThirdButtonDown = 1 << 7,

            /// <summary>
            /// Third button released this frame.
            /// </summary>
            ThirdButtonUp = 1 << 8,

            /// <summary>
            /// Analogous to the first extended button button down.
            /// A <see cref="TouchPointer"/>, <see cref="PenPointer"/> or <see cref="ObjectPointer"/> does not use this flag.
            /// A <see cref="MousePointer"/> has this flag set when the first extended button is down.
            /// </summary>
            FourthButtonPressed = 1 << 9,

            /// <summary>
            /// Fourth button pressed this frame.
            /// </summary>
            FourthButtonDown = 1 << 10,

            /// <summary>
            /// Fourth button released this frame.
            /// </summary>
            FourthButtonUp = 1 << 11,

            /// <summary>
            /// Analogous to the second extended button button down.
            /// A <see cref="TouchPointer"/>, <see cref="PenPointer"/> or <see cref="ObjectPointer"/> does not use this flag.
            /// A <see cref="MousePointer"/> has this flag set when the second extended button is down.
            /// </summary>
            FifthButtonPressed = 1 << 12,

            /// <summary>
            /// Fifth button pressed this frame.
            /// </summary>
            FifthButtonDown = 1 << 13,

            /// <summary>
            /// Fifth button released this frame.
            /// </summary>
            FifthButtonUp = 1 << 14,

            /// <summary>
            /// Any button is pressed.
            /// </summary>
            AnyButtonPressed = FirstButtonPressed | SecondButtonPressed | ThirdButtonPressed | FourthButtonPressed | FifthButtonPressed,

            /// <summary>
            /// Any button down this frame.
            /// </summary>
            AnyButtonDown = FirstButtonDown | SecondButtonDown | ThirdButtonDown | FourthButtonDown | FifthButtonDown,

            /// <summary>
            /// Any button up this frame.
            /// </summary>
            AnyButtonUp = FirstButtonUp | SecondButtonUp | ThirdButtonUp | FourthButtonUp | FifthButtonUp
        }

        #endregion

        #region Public properties

        /// <inheritdoc />
        public int Id { get; private set; }

        /// <inheritdoc />
        public PointerType Type { get; protected set; }

        public PointerButtonState Buttons { get; set; }

        /// <summary>
        /// Original input source which created this pointer.
        /// <seealso cref="IInputSource"/>
        /// </summary>
        public IInputSource InputSource { get; private set; }

        /// <summary>
        /// <para>Current position in screen coordinates.</para>
        /// <para>Note: setting this property doesn't immediately change its value, the value actually changes during the next TouchManager update phase.</para>
        /// </summary>
        public Vector2 Position
        {
            get { return position; }
            set { newPosition = value; }
        }

        /// <summary>
        /// Previous <see cref="Position"/> (during last frame) in screen coordinates.
        /// </summary>
        public Vector2 PreviousPosition { get; private set; }

        /// <summary>
        /// <para>Gets or sets pointer flags: <see cref="FLAG_ARTIFICIAL"/>.</para>
        /// <para>Note: setting this property doesn't immediately change its value, the value actually changes during the next TouchManager update phase.</para>
        /// </summary>
        public uint Flags { get; set; }

        /// <summary>
        /// Projection parameters for the layer which created this pointer.
        /// </summary>
        public ProjectionParams ProjectionParams
        {
            get
            {
                if (pressData.Layer == null) return null;
                return pressData.Layer.GetProjectionParams(this);
            }
        }

        #endregion

        #region Private variables

        private static StringBuilder builder;

        private TouchManagerInstance manager;
        private int refCount = 0;
        private Vector2 position, newPosition;
        private HitData pressData, overData;
        private bool overDataIsDirty = true;

        #endregion

        #region Public methods

        /// <inheritdoc />
        public HitData GetOverData(bool forceRecalculate = false)
        {
            if (overDataIsDirty || forceRecalculate)
            {
                manager.INTERNAL_GetHitTarget(this, out overData);
                overDataIsDirty = false;
            }
            return overData;
        }

        /// <summary>
        /// Returns <see cref="HitData"/> when the pointer was pressed. If the pointer is not pressed uninitialized <see cref="HitData"/> is returned.
        /// </summary>
        public HitData GetPressData()
        {
            return pressData;
        }

        /// <summary>
        /// Copies values from <see cref="target"/>.
        /// </summary>
        /// <param name="target">The target pointer to copy values from.</param>
        public virtual void CopyFrom(Pointer target)
        {
            Type = target.Type;
            Flags = target.Flags;
            Buttons = target.Buttons;
            position = target.position;
            newPosition = target.newPosition;
            PreviousPosition = target.PreviousPosition;
        }

        /// <inheritdoc />
        public override bool Equals(object other)
        {
            return Equals(other as Pointer);
        }

        /// <inheritdoc />
        public bool Equals(Pointer other)
        {
            if (other == null)
                return false;

            return Id == other.Id;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Id;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (builder == null) builder = new StringBuilder();
            builder.Length = 0;
            builder.Append("(Pointer type: ");
            builder.Append(Type);
            builder.Append(", id: ");
            builder.Append(Id);
            builder.Append(", flags: ");
            BinaryUtils.ToBinaryString(Flags, builder, 8);
            //            builder.Append(", buttons: ");
            //            builder.Append((uint)Buttons);
            builder.Append(", position: ");
            builder.Append(Position);
            builder.Append(")");
            return builder.ToString();
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="Pointer"/> class.
        /// </summary>
        public Pointer(IInputSource input)
        {
            manager = TouchManager.Instance as TouchManagerInstance;
            Type = PointerType.Unknown;
            InputSource = input;
            INTERNAL_Reset();
        }

        #endregion

        #region Internal methods

        internal virtual void INTERNAL_Init(int id)
        {
            Id = id;
            PreviousPosition = position = newPosition;
        }

        internal virtual void INTERNAL_Reset()
        {
            Id = INVALID_POINTER;
            INTERNAL_ClearPressData();
            position = newPosition = PreviousPosition = Vector2.zero;
            Flags = 0;
            Buttons = PointerButtonState.Nothing;
            overDataIsDirty = true;
        }

        internal virtual void INTERNAL_FrameStarted()
        {
            Buttons &= ~(PointerButtonState.AnyButtonDown | PointerButtonState.AnyButtonUp);
            overDataIsDirty = true;
        }

        internal virtual void INTERNAL_UpdatePosition()
        {
            PreviousPosition = position;
            position = newPosition;
        }

        internal void INTERNAL_Retain()
        {
            refCount++;
        }

        internal int INTERNAL_Release()
        {
            return --refCount;
        }

        internal void INTERNAL_SetPressData(HitData data)
        {
            pressData = data;
            overData = data;
            overDataIsDirty = false;
        }

        internal void INTERNAL_ClearPressData()
        {
            pressData = default(HitData);
            refCount = 0;
        }

        #endregion
    }
}