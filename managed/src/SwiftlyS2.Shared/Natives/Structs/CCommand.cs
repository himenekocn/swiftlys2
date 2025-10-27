using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SwiftlyS2.Shared.Natives;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct CCommand
{
    private const int COMMAND_MAX_ARGC = 64;
    private const int COMMAND_MAX_LENGTH = 512;

    private int _argv0Size;
    private CUtlVectorFixedGrowable<byte> _argSBuffer;
    private CUtlVectorFixedGrowable<byte> _argvBuffer;
    private CUtlVectorFixedGrowable<nint> _args;

    public CCommand()
    {
        _argv0Size = 0;
        _argSBuffer = new CUtlVectorFixedGrowable<byte>(COMMAND_MAX_LENGTH);
        _argvBuffer = new CUtlVectorFixedGrowable<byte>(COMMAND_MAX_LENGTH);
        _args = new CUtlVectorFixedGrowable<nint>(COMMAND_MAX_ARGC);
        EnsureBuffers();
        Reset();
    }

    public CCommand(int argc, nint ppArgV)
    {
        _argv0Size = 0;
        _argSBuffer = new CUtlVectorFixedGrowable<byte>(COMMAND_MAX_LENGTH);
        _argvBuffer = new CUtlVectorFixedGrowable<byte>(COMMAND_MAX_LENGTH);
        _args = new CUtlVectorFixedGrowable<nint>(COMMAND_MAX_ARGC);
        EnsureBuffers();
        Reset();

        byte* pBuf = (byte*)_argvBuffer.Base;
        byte* pSBuf = (byte*)_argSBuffer.Base;
        nint* ppArgVPtr = (nint*)ppArgV;

        for (int i = 0; i < argc; ++i)
        {
            _args.AddToTail((nint)pBuf);

            byte* pArg = (byte*)ppArgVPtr[i];
            int nLen = StrLen(pArg);

            Unsafe.CopyBlock(pBuf, pArg, (uint)(nLen + 1));

            if (i == 0)
            {
                _argv0Size = nLen;
            }
            pBuf += nLen + 1;

            bool bContainsSpace = StrChr(pArg, (byte)' ') != null;
            if (bContainsSpace)
            {
                *pSBuf++ = (byte)'"';
            }
            Unsafe.CopyBlock(pSBuf, pArg, (uint)nLen);
            pSBuf += nLen;
            if (bContainsSpace)
            {
                *pSBuf++ = (byte)'"';
            }

            if (i != argc - 1)
            {
                *pSBuf++ = (byte)' ';
            }
        }
    }

    private void EnsureBuffers()
    {
        _argSBuffer.SetSize(MaxCommandLength());
        _argvBuffer.SetSize(MaxCommandLength());
    }

    public void Reset()
    {
        _argv0Size = 0;
        ((byte*)_argSBuffer.Base)[0] = 0;
        _args.RemoveAll();
    }

    public bool Tokenize(CUtlString command, characterset_t* breakSet = null)
    {
        if (_argSBuffer.Count == 0)
            EnsureBuffers();

        Reset();

        string cmdStr = command;
        if (string.IsNullOrEmpty(cmdStr))
            return false;

        if (breakSet == null)
        {
            breakSet = DefaultBreakSet();
        }

        int nLen = cmdStr.Length;
        if (nLen >= _argSBuffer.Count - 1)
        {
            return false;
        }

        byte* pDest = (byte*)_argSBuffer.Base;
        fixed (char* pSrc = cmdStr)
        {
            for (int i = 0; i < nLen; i++)
            {
                pDest[i] = (byte)pSrc[i];
            }
            pDest[nLen] = 0;
        }

        return true;
    }

    public int ArgC()
    {
        return _args.Count;
    }

    public nint ArgV()
    {
        return ArgC() != 0 ? _args.Base : 0;
    }

    public nint ArgS()
    {
        return _argv0Size != 0 ? _argSBuffer.Base + _argv0Size : 0;
    }

    public nint GetCommandString()
    {
        return ArgC() != 0 ? _argSBuffer.Base : 0;
    }

    public nint Arg(int index)
    {
        if (index < 0 || index >= ArgC())
            return 0;
        return (nint)_args[index];
    }

    public nint this[int index] => Arg(index);

    public int FindArg(nint pName)
    {
        int nArgC = ArgC();
        for (int i = 1; i < nArgC; i++)
        {
            if (StrICmpFast((byte*)Arg(i), (byte*)pName))
                return (i + 1) < nArgC ? i + 1 : -1;
        }
        return -1;
    }

    public int FindArgInt(nint pName, int defaultVal)
    {
        int idx = FindArg(pName);
        if (idx != -1)
            return Atoi((byte*)(nint)_args[idx]);
        else
            return defaultVal;
    }

    public static int MaxCommandLength()
    {
        return COMMAND_MAX_LENGTH - 1;
    }

    public static characterset_t* DefaultBreakSet()
    {
        return null;
    }

    private static int StrLen(byte* str)
    {
        int len = 0;
        while (str[len] != 0) len++;
        return len;
    }

    private static byte* StrChr(byte* str, byte ch)
    {
        while (*str != 0)
        {
            if (*str == ch) return str;
            str++;
        }
        return null;
    }

    private static bool StrICmpFast(byte* s1, byte* s2)
    {
        while (*s1 != 0 && *s2 != 0)
        {
            byte c1 = *s1;
            byte c2 = *s2;
            if (c1 >= 'A' && c1 <= 'Z') c1 += 32;
            if (c2 >= 'A' && c2 <= 'Z') c2 += 32;
            if (c1 != c2) return false;
            s1++;
            s2++;
        }
        return *s1 == *s2;
    }

    private static int Atoi(byte* str)
    {
        int result = 0;
        bool negative = false;

        while (*str == ' ') str++;

        if (*str == '-')
        {
            negative = true;
            str++;
        }
        else if (*str == '+')
        {
            str++;
        }

        while (*str >= '0' && *str <= '9')
        {
            result = result * 10 + (*str - '0');
            str++;
        }

        return negative ? -result : result;
    }
}