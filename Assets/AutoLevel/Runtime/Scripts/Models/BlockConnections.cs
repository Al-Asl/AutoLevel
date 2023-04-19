using System;

[System.Serializable]
public struct BlockConnection
{
    public int left;
    public int down;
    public int backward;
    public int right;
    public int up;
    public int forward;

    public void ApplyAction(VariantAction action)
    {
        int temp = 0;
        switch (action)
        {
            case VariantAction.RotateX:
                temp = down;
                down = forward;
                forward = up;
                up = backward;
                backward = temp;
                break;
            case VariantAction.RotateY:
                temp = left;
                left = backward;
                backward = right;
                right = forward;
                forward = temp;
                break;
            case VariantAction.RotateZ:
                temp = left;
                left = up;
                up = right;
                right = down;
                down = temp;
                break;
            case VariantAction.MirrorX:
                temp = left;
                left = right;
                right = temp;
                break;
            case VariantAction.MirrorY:
                temp = down;
                down = up;
                up = temp;
                break;
            case VariantAction.MirrorZ:
                temp = backward;
                backward = forward;
                forward = temp;
                break;
        }
    }

    public override int GetHashCode()
    {
        return new XXHash().Append(left).Append(down).Append(backward).Append(right).Append(up).Append(forward);
    }

    public int this[int dir]
    {
        get
        {
            switch (dir)
            {
                case 0:
                    return left;
                case 1:
                    return down;
                case 2:
                    return backward;
                case 3:
                    return right;
                case 4:
                    return up;
                case 5:
                    return forward;
            }
            throw new System.Exception();
        }
        set
        {
            switch (dir)
            {
                case 0:
                    left = value;
                    return;
                case 1:
                    down = value;
                    return;
                case 2:
                    backward = value;
                    return;
                case 3:
                    right = value;
                    return;
                case 4:
                    up = value;
                    return;
                case 5:
                    forward = value;
                    return;
            }
            throw new System.Exception();
        }
    }
}