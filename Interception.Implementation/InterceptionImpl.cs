using System.Runtime.InteropServices;
using static Interception.InterceptionInterop;

namespace Interception;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
public static unsafe class InterceptionImpl
{
    static int keyboardDeviceID, mouseDeviceID;
    static nint keyboardContext, mouseContext;

    public static int MouseX { get; private set; }
    public static int MouseY { get; private set; }
    public static bool IsLeftMouseDown { get; private set; }
    public static bool IsRightMouseDown{ get; private set; }

    static InterceptionImpl()
    {

#if DEBUG
        return;
#endif

        keyboardContext = interception_create_context();
        interception_set_filter(keyboardContext, interception_is_keyboard, Filter.All);

        mouseContext = interception_create_context();
        interception_set_filter(mouseContext, interception_is_mouse, Filter.All);

        new Thread(DriverKeyboardUpdater)
        {
            Priority = ThreadPriority.Highest
        }.Start();

        new Thread(DriverMouseUpdater)
        {
            Priority = ThreadPriority.Highest
        }.Start();
    }

    static void DriverKeyboardUpdater()
    {
        var stroke = stackalloc Stroke[1];
        while (true)
        {
            try
            {
                while (interception_receive(keyboardContext, keyboardDeviceID = interception_wait(keyboardContext), stroke, 1) > 0)
                {
                    var strokeKey = stroke->Key;
                    var key = ToKey(strokeKey);
                    var processed = false;
                    if (strokeKey.IsKeyDown)
                    {
                        switch (IsKeyUp(key))
                        {
                            case true:
                                SetKeyIsDown(key);
                                processed = InternalOnKeyDown(key, false);
                                break;
                            case false:
                                processed = InternalOnKeyDown(key, true);
                                break;
                        }
                    }
                    else
                    {
                        SetKeyIsUp(key);
                        processed = InternalOnKeyUp(key);
                    }

                    if (!processed)
                        interception_send(keyboardContext, keyboardDeviceID, stroke, 1);
                }
            }
            catch 
            {
                try
                {
                    interception_send(keyboardContext, keyboardDeviceID, stroke, 1);
                }
                catch { }
            }
        }
    }

    static void DriverMouseUpdater()
    {
        var stroke = stackalloc Stroke[1];
        while (true)
        {
            try
            {
                while (true)
                {
                    interception_receive(mouseContext, mouseDeviceID = interception_wait(mouseContext), stroke, 1);

                    var processed = false;
                    switch (stroke->Mouse.State)
                    {
                        case MouseState.LeftButtonDown:
                            IsLeftMouseDown = true;
                            SetKeyIsDown(Key.MouseLeft);
                            processed = InternalOnKeyDown(Key.MouseLeft, false);
                            break;
                        case MouseState.RightButtonDown:
                            IsRightMouseDown = true;
                            SetKeyIsDown(Key.MouseRight);
                            processed = InternalOnKeyDown(Key.MouseRight, false);
                            break;
                        case MouseState.MiddleButtonDown:
                            SetKeyIsDown(Key.MouseMiddle);
                            processed = InternalOnKeyDown(Key.MouseMiddle, false);
                            break;
                        case MouseState.Button4Down:
                            SetKeyIsDown(Key.Button1);
                            processed = InternalOnKeyDown(Key.Button1, false);
                            break;
                        case MouseState.Button5Down:
                            SetKeyIsDown(Key.Button2);
                            processed = InternalOnKeyDown(Key.Button2, false);
                            break;
                        case MouseState.LeftButtonUp:
                            IsLeftMouseDown = false;
                            SetKeyIsUp(Key.MouseLeft);
                            processed = InternalOnKeyUp(Key.MouseLeft);
                            break;
                        case MouseState.RightButtonUp:
                            IsRightMouseDown = false;
                            SetKeyIsUp(Key.MouseRight);
                            processed = InternalOnKeyUp(Key.MouseRight);
                            break;
                        case MouseState.MiddleButtonUp:
                            SetKeyIsUp(Key.MouseMiddle);
                            processed = InternalOnKeyUp(Key.MouseMiddle);
                            break;
                        case MouseState.Button4Up:
                            SetKeyIsUp(Key.Button1);
                            processed = InternalOnKeyUp(Key.Button1);
                            break;
                        case MouseState.Button5Up:
                            SetKeyIsUp(Key.Button2);
                            processed = InternalOnKeyUp(Key.Button2);
                            break;
                        case MouseState.Wheel:
                            processed = InternalOnMouseWheel(stroke->Mouse.Rolling);
                            break;
                        default:
                            processed = InternalOnMouseMove(stroke->Mouse.X, stroke->Mouse.Y);
                            break;
                    }

                    if (!processed)
                        interception_send(mouseContext, mouseDeviceID, stroke, 1);
                }
            }
            catch 
            {
                try
                {
                    interception_send(mouseContext, mouseDeviceID, stroke, 1);
                } catch { }
            }
        }
    }

    static long* keyStates = (long*)Marshal.AllocCoTaskMem(0x80) + 0x08;

    static void SetKeyIsDown(Key key) => SetKeyIsDown((int)key);
    static void SetKeyIsDown(int key) => keyStates[key / 64] |= 1L << (key % 64);

    static void SetKeyIsUp(Key key) => SetKeyIsUp((int)key);
    static void SetKeyIsUp(int key) => keyStates[key / 64] &= ~(1 << (key % 64));

    static Key ToKey(KeyStroke keyStroke)
    {
        var result = keyStroke.Code;
        if ((keyStroke.State & KeyState.E0) != 0)
            result += 0x100;
        return (Key)result;
    }

    static KeyStroke ToKeyStroke(Key key, bool down)
    {
        var result = new KeyStroke();
        if (!down)
            result.State = KeyState.Up;
        var code = (short)key;
        if (code >= 0x100)
        {
            code -= 0x100;
            result.State |= KeyState.E0;
        }
        else if (code < 0)
        {
            code += 100;
            result.State |= KeyState.E0;
        }
        result.Code = (ushort)code;
        return result;
    }

    public delegate bool OnMouseMoveDelegate(int x, int y);
    public static OnMouseMoveDelegate? OnMouseMove;

    public delegate bool OnMouseWheelDelegate(int rolling);
    public static OnMouseWheelDelegate? OnMouseWheel;

    public delegate bool OnKeyDownDelegate(Key key, bool repeat);
    public static OnKeyDownDelegate? OnKeyDown;

    public delegate bool OnKeyUpDelegate(Key key);
    public static OnKeyUpDelegate? OnKeyUp;

    static bool InternalOnMouseMove(int x, int y)
    {
        (MouseX, MouseY) = (x, y);
        if (OnMouseMove != null)
            if (x != 0 || y != 0)
                return OnMouseMove(x, y);
        return false;
    }

    static bool InternalOnMouseWheel(int rolling)
    {
        if (OnMouseWheel != null)
            return OnMouseWheel(rolling);
        return false;
    }

    static bool InternalOnKeyDown(Key key, bool repeat)
    {
        if (OnKeyDown != null)
            return OnKeyDown(key, repeat);
        return false;
    }

    static bool InternalOnKeyUp(Key key)
    {
        if (OnKeyUp != null)
            return OnKeyUp(key);
        return false;
    }

    public static bool IsKeyDown(Key key) => IsKeyDown((int)key);
    static bool IsKeyDown(int key) => (keyStates[key / 64] & (1 << (key % 64))) != 0;

    public static bool IsKeyUp(Key key) => !IsKeyDown(key);

    public static void KeyUp(params Key[] keys)
    {
        foreach (var key in keys)
        {
            SetKeyIsUp(key);
            if (((short)key) < 0)
            {
                var stroke = new Stroke();
                switch (key)
                {
                    case Key.MouseLeft:
                        stroke.Mouse.State = MouseState.LeftButtonUp;
                        break;
                    case Key.MouseRight:
                        stroke.Mouse.State = MouseState.RightButtonUp;
                        break;
                    case Key.MouseMiddle:
                        stroke.Mouse.State = MouseState.MiddleButtonUp;
                        break;
                    case Key.Button1:
                        stroke.Mouse.State = MouseState.Button4Up;
                        break;
                    case Key.Button2:
                        stroke.Mouse.State = MouseState.Button5Up;
                        break;
                }
                interception_send(mouseContext, mouseDeviceID, &stroke, 1);
            }
            else
            {
                var stroke = new Stroke();
                stroke.Key = ToKeyStroke(key, false);
                interception_send(keyboardContext, keyboardDeviceID, &stroke, 1);
            }
        }
    }

    public static void KeyDown(params Key[] keys)
    {
        foreach (var key in keys)
        {
            SetKeyIsDown(key);

            if (((short)key) < 0)
            {
                var stroke = new Stroke();
                switch (key)
                {
                    case Key.MouseLeft:
                        stroke.Mouse.State = MouseState.LeftButtonDown;
                        break;
                    case Key.MouseRight:
                        stroke.Mouse.State = MouseState.RightButtonDown;
                        break;
                    case Key.MouseMiddle:
                        stroke.Mouse.State = MouseState.MiddleButtonDown;
                        break;
                    case Key.Button1:
                        stroke.Mouse.State = MouseState.Button4Down;
                        break;
                    case Key.Button2:
                        stroke.Mouse.State = MouseState.Button5Down;
                        break;
                }
                interception_send(mouseContext, mouseDeviceID, &stroke, 1);
            }
            else
            {
                var stroke = new Stroke();
                stroke.Key = ToKeyStroke(key, true);
                interception_send(keyboardContext, keyboardDeviceID, &stroke, 1);
            }
        }
    }

    public static void MouseScroll(int deviceID, short rolling)
    {
        var stroke = new Stroke();
        stroke.Mouse.State = MouseState.Wheel;
        stroke.Mouse.Rolling = rolling;
        interception_send(mouseContext, deviceID, &stroke, 1);
    }

    public static void MouseScroll(short rolling) => MouseScroll(mouseDeviceID, rolling);

    public static void MouseMove(int deviceID, int x, int y)
    {
        var stroke = new Stroke();
        stroke.Mouse.X = x;
        stroke.Mouse.Y = y;
        stroke.Mouse.Flags = MouseFlag.MoveRelative;
        interception_send(mouseContext, deviceID, &stroke, 1);
    }

    public static void MouseSet(int deviceID, int x, int y)
    {
        var stroke = new Stroke();
        stroke.Mouse.X = x;
        stroke.Mouse.Y = y;
        stroke.Mouse.Flags = MouseFlag.MoveAbsolute;
        interception_send(mouseContext, deviceID, &stroke, 1);
    }

    public static void MouseMove(int x, int y) => MouseMove(mouseDeviceID, x, y);

    public static void MouseSet(int x, int y) => MouseSet(mouseDeviceID, x, y);
}