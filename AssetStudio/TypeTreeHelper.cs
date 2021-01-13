using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;

namespace AssetStudio
{
    public static class TypeTreeHelper
    {
        public static void ReadTypeString(StringBuilder sb, List<TypeTreeNode> members, ObjectReader reader)
        {
            reader.Reset();
            for (var i = 0; i < members.Count; i++)
            {
                ReadStringValue(sb, members, reader, ref i);
            }

            var read = reader.Position - reader.byteStart;
            if (read != reader.byteSize)
            {
                Logger.Error($"Error while read type, read {read} bytes but expected {reader.byteSize} bytes");
            }
        }

        private static void ReadStringValue(StringBuilder sb, List<TypeTreeNode> members, BinaryReader reader,
            ref int i)
        {
            var member = members[i];
            var level = member.m_Level;
            var varTypeStr = member.m_Type;
            var varNameStr = member.m_Name;
            object value = null;
            var append = true;
            var align = (member.m_MetaFlag & 0x4000) != 0;
            switch (varTypeStr)
            {
                case "SInt8":
                    value = reader.ReadSByte();
                    break;
                case "UInt8":
                case "char":
                    value = reader.ReadByte();
                    break;
                case "short":
                case "SInt16":
                    value = reader.ReadInt16();
                    break;
                case "UInt16":
                case "unsigned short":
                    value = reader.ReadUInt16();
                    break;
                case "int":
                case "SInt32":
                    value = reader.ReadInt32();
                    break;
                case "UInt32":
                case "unsigned int":
                case "Type*":
                    value = reader.ReadUInt32();
                    break;
                case "long long":
                case "SInt64":
                    value = reader.ReadInt64();
                    break;
                case "UInt64":
                case "unsigned long long":
                case "FileSize":
                    value = reader.ReadUInt64();
                    break;
                case "float":
                    value = reader.ReadSingle();
                    break;
                case "double":
                    value = reader.ReadDouble();
                    break;
                case "bool":
                    value = reader.ReadBoolean();
                    break;
                case "string":
                    append = false;
                    var str = reader.ReadAlignedString();
                    sb.AppendFormat("{0}{1} {2} = \"{3}\"\r\n", (new string('\t', level)), varTypeStr, varNameStr, str);
                    i += 3;
                    break;
                case "map":
                {
                    if ((members[i + 1].m_MetaFlag & 0x4000) != 0)
                        align = true;
                    append = false;
                    sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level)), varTypeStr, varNameStr);
                    sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level + 1)), "Array", "Array");
                    var size = reader.ReadInt32();
                    sb.AppendFormat("{0}{1} {2} = {3}\r\n", (new string('\t', level + 1)), "int", "size", size);
                    var map = GetMembers(members, i);
                    i += map.Count - 1;
                    var first = GetMembers(map, 4);
                    var next = 4 + first.Count;
                    var second = GetMembers(map, next);
                    for (var j = 0; j < size; j++)
                    {
                        sb.AppendFormat("{0}[{1}]\r\n", (new string('\t', level + 2)), j);
                        sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level + 2)), "pair", "data");
                        var tmp1 = 0;
                        var tmp2 = 0;
                        ReadStringValue(sb, first, reader, ref tmp1);
                        ReadStringValue(sb, second, reader, ref tmp2);
                    }

                    break;
                }
                case "TypelessData":
                {
                    append = false;
                    var size = reader.ReadInt32();
                    reader.ReadBytes(size);
                    i += 2;
                    sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level)), varTypeStr, varNameStr);
                    sb.AppendFormat("{0}{1} {2} = {3}\r\n", (new string('\t', level)), "int", "size", size);
                    break;
                }
                default:
                {
                    if (i < members.Count - 1 && members[i + 1].m_Type == "Array") //Array
                    {
                        if ((members[i + 1].m_MetaFlag & 0x4000) != 0)
                            align = true;
                        append = false;
                        sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level)), varTypeStr, varNameStr);
                        sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level + 1)), "Array", "Array");
                        var size = reader.ReadInt32();
                        sb.AppendFormat("{0}{1} {2} = {3}\r\n", (new string('\t', level + 1)), "int", "size", size);
                        var vector = GetMembers(members, i);
                        i += vector.Count - 1;
                        for (var j = 0; j < size; j++)
                        {
                            sb.AppendFormat("{0}[{1}]\r\n", (new string('\t', level + 2)), j);
                            var tmp = 3;
                            ReadStringValue(sb, vector, reader, ref tmp);
                        }

                        break;
                    }
                    else //Class
                    {
                        append = false;
                        sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level)), varTypeStr, varNameStr);
                        var @class = GetMembers(members, i);
                        i += @class.Count - 1;
                        for (var j = 1; j < @class.Count; j++)
                        {
                            ReadStringValue(sb, @class, reader, ref j);
                        }

                        break;
                    }
                }
            }

            if (append)
                sb.AppendFormat("{0}{1} {2} = {3}\r\n", (new string('\t', level)), varTypeStr, varNameStr, value);
            if (align)
                reader.AlignStream();
        }

        public static OrderedDictionary ReadType(List<TypeTreeNode> members, ObjectReader reader)
        {
            reader.Reset();
            var obj = new OrderedDictionary();
            for (var i = 1; i < members.Count; i++)
            {
                var member = members[i];
                var varNameStr = member.m_Name;
                obj[varNameStr] = ReadValue(members, reader, ref i);
            }

            var read = reader.Position - reader.byteStart;
            if (read != reader.byteSize)
            {
                Logger.Error($"Error while read type, read {read} bytes but expected {reader.byteSize} bytes");
            }

            return obj;
        }

        private static object ReadValue(List<TypeTreeNode> members, BinaryReader reader, ref int i)
        {
            var member = members[i];
            var varTypeStr = member.m_Type;
            object value;
            var align = (member.m_MetaFlag & 0x4000) != 0;
            switch (varTypeStr)
            {
                case "SInt8":
                    value = reader.ReadSByte();
                    break;
                case "UInt8":
                case "char":
                    value = reader.ReadByte();
                    break;
                case "short":
                case "SInt16":
                    value = reader.ReadInt16();
                    break;
                case "UInt16":
                case "unsigned short":
                    value = reader.ReadUInt16();
                    break;
                case "int":
                case "SInt32":
                    value = reader.ReadInt32();
                    break;
                case "UInt32":
                case "unsigned int":
                case "Type*":
                    value = reader.ReadUInt32();
                    break;
                case "long long":
                case "SInt64":
                    value = reader.ReadInt64();
                    break;
                case "UInt64":
                case "unsigned long long":
                case "FileSize":
                    value = reader.ReadUInt64();
                    break;
                case "float":
                    value = reader.ReadSingle();
                    break;
                case "double":
                    value = reader.ReadDouble();
                    break;
                case "bool":
                    value = reader.ReadBoolean();
                    break;
                case "string":
                    value = reader.ReadAlignedString();
                    i += 3;
                    break;
                case "map":
                {
                    if ((members[i + 1].m_MetaFlag & 0x4000) != 0)
                        align = true;
                    var map = GetMembers(members, i);
                    i += map.Count - 1;
                    var first = GetMembers(map, 4);
                    var next = 4 + first.Count;
                    var second = GetMembers(map, next);
                    var size = reader.ReadInt32();
                    var dic = new List<KeyValuePair<object, object>>(size);
                    for (var j = 0; j < size; j++)
                    {
                        var tmp1 = 0;
                        var tmp2 = 0;
                        dic.Add(new KeyValuePair<object, object>(ReadValue(first, reader, ref tmp1),
                            ReadValue(second, reader, ref tmp2)));
                    }

                    value = dic;
                    break;
                }
                case "TypelessData":
                {
                    var size = reader.ReadInt32();
                    value = reader.ReadBytes(size);
                    i += 2;
                    break;
                }
                default:
                {
                    if (i < members.Count - 1 && members[i + 1].m_Type == "Array") //Array
                    {
                        if ((members[i + 1].m_MetaFlag & 0x4000) != 0)
                            align = true;
                        var vector = GetMembers(members, i);
                        i += vector.Count - 1;
                        var size = reader.ReadInt32();

                        // TODO: Async export
                        //   - PathIDs are all common ones (prefixed with CMN_)
                        // if (reader is ObjectReader objectReader && 
                        //     (objectReader.m_PathID == -793041912120237419 
                        //      // || objectReader.m_PathID == -8265595475037570603 
                        //      // || objectReader.m_PathID == -1097766641109887934
                        //      // || objectReader.m_PathID == -4439694546364576038
                        //      || objectReader.m_PathID == -3667104045530355278))
                        // {
                        //     Console.WriteLine($"{size} - {objectReader.m_PathID} - {objectReader.Position} - {objectReader.byteStart} #{reader.GetHashCode()} i={i} vector={vector.Count}");
                        // }

                        // if (reader is ObjectReader objectReader && size > 10000000)
                        // {
                        //     Console.WriteLine($"{size} - {objectReader.m_PathID} - {objectReader.Position} - {objectReader.byteStart} #{reader.GetHashCode()} i={i} vector={vector.Count}");
                        // }

                        /*
                         Expected outputs
                         
                         2021-01-13T04:07:26.691236: Exporting AnimatorOverrideController: bow_11036801
                         2021-01-13T04:07:30.280978: Exporting AnimatorOverrideController: bow_win_11036801
                         2021-01-13T04:07:36.805653: Exporting AnimatorOverrideController: rod_11036101
                         2021-01-13T04:07:37.602897: Exporting AnimatorOverrideController: rod_win_11036101
                         0 - -8265595475037570603 - 412580 - 412544
                         0 - -8265595475037570603 - 412584 - 412544
                         0 - -8265595475037570603 - 412588 - 412544
                         0 - -8265595475037570603 - 412592 - 412544
                         0 - -8265595475037570603 - 412596 - 412544
                         0 - -8265595475037570603 - 412600 - 412544
                         0 - -8265595475037570603 - 412604 - 412544
                         4 - -8265595475037570603 - 412712 - 412544
                         20 - -8265595475037570603 - 413012 - 412544
                         20 - -8265595475037570603 - 413152 - 412544
                         55 - -8265595475037570603 - 413252 - 412544
                         21 - -8265595475037570603 - 413476 - 412544
                         2 - -8265595475037570603 - 413904 - 412544
                         288 - -8265595475037570603 - 413936 - 412544
                         208 - -8265595475037570603 - 415092 - 412544
                         200 - -8265595475037570603 - 415952 - 412544
                         224 - -8265595475037570603 - 416756 - 412544
                         0 - -8265595475037570603 - 418552 - 412544
                         66 - -8265595475037570603 - 418568 - 412544
                         0 - -8265595475037570603 - 420420 - 412544
                         0 - -8265595475037570603 - 420428 - 412544

                         2021-01-13T04:25:37.941906: Exporting AnimatorOverrideController: bow_win_11036801
                         2021-01-13T04:25:37.471683: Exporting AnimatorOverrideController: bow_11036801
                         0 - -793041912120237419 - 3628128 - 3628096 #63037959 i=50 vector=43
                         0 - -793041912120237419 - 3628132 - 3628096 #63037959 i=82 vector=32
                         0 - -793041912120237419 - 3628136 - 3628096 #63037959 i=120 vector=38
                         0 - -793041912120237419 - 3628140 - 3628096 #63037959 i=158 vector=38
                         0 - -793041912120237419 - 3628144 - 3628096 #63037959 i=196 vector=38
                         0 - -793041912120237419 - 3628148 - 3628096 #63037959 i=227 vector=31
                         0 - -793041912120237419 - 3628152 - 3628096 #63037959 i=251 vector=24
                         4 - -793041912120237419 - 3628260 - 3628096 #63037959 i=48 vector=25
                         20 - -793041912120237419 - 3628560 - 3628096 #63037959 i=18 vector=4
                         20 - -793041912120237419 - 3628700 - 3628096 #63037959 i=18 vector=4
                         55 - -793041912120237419 - 3628800 - 3628096 #63037959 i=98 vector=4
                         21 - -793041912120237419 - 3629024 - 3628096 #63037959 i=105 vector=7
                         1800 - -793041912120237419 - 3629452 - 3628096 #63037959 i=4 vector=4
                         930 - -793041912120237419 - 3636676 - 3628096 #63037959 i=8 vector=4
                         127 - -793041912120237419 - 3640400 - 3628096 #63037959 i=4 vector=4
                         200 - -793041912120237419 - 3640936 - 3628096 #63037959 i=198 vector=4
                         224 - -793041912120237419 - 3641740 - 3628096 #63037959 i=204 vector=6
                         0 - -793041912120237419 - 3643536 - 3628096 #63037959 i=208 vector=4
                         66 - -793041912120237419 - 3643552 - 3628096 #63037959 i=12 vector=12
                         0 - -793041912120237419 - 3645404 - 3628096 #63037959 i=18 vector=6
                         0 - -793041912120237419 - 3645412 - 3628096 #63037959 i=523 vector=19

                         2021-01-13T04:39:55.757339: Exporting AnimatorOverrideController: bow_11036801
                         2021-01-13T04:39:56.867794: Exporting AnimatorOverrideController: bow_win_11036801
                         2021-01-13T04:40:03.377014: Exporting AnimatorOverrideController: rod_11036101
                         2021-01-13T04:40:03.944508: Exporting AnimatorOverrideController: rod_win_11036101
                         0 - -1097766641109887934 - 3549700 - 3549664
                         0 - -1097766641109887934 - 3549704 - 3549664
                         0 - -1097766641109887934 - 3549708 - 3549664
                         0 - -1097766641109887934 - 3549712 - 3549664
                         0 - -1097766641109887934 - 3549716 - 3549664
                         0 - -1097766641109887934 - 3549720 - 3549664
                         0 - -1097766641109887934 - 3549724 - 3549664
                         4 - -1097766641109887934 - 3549832 - 3549664
                         20 - -1097766641109887934 - 3550132 - 3549664
                         20 - -1097766641109887934 - 3550272 - 3549664
                         55 - -1097766641109887934 - 3550372 - 3549664
                         21 - -1097766641109887934 - 3550596 - 3549664
                         156 - -1097766641109887934 - 3551024 - 3549664
                         124 - -1097766641109887934 - 3551672 - 3549664
                         216 - -1097766641109887934 - 3552172 - 3549664
                         200 - -1097766641109887934 - 3553064 - 3549664
                         224 - -1097766641109887934 - 3553868 - 3549664
                         0 - -1097766641109887934 - 3555664 - 3549664
                         66 - -1097766641109887934 - 3555680 - 3549664
                         0 - -1097766641109887934 - 3557532 - 3549664
                         0 - -1097766641109887934 - 3557540 - 3549664

                         2021-01-13T04:59:00.213503: Exporting AnimatorOverrideController: bow_11036801
                         0 - -4439694546364576038 - 2217568 - 2217536
                         0 - -4439694546364576038 - 2217572 - 2217536
                         0 - -4439694546364576038 - 2217576 - 2217536
                         0 - -4439694546364576038 - 2217580 - 2217536
                         0 - -4439694546364576038 - 2217584 - 2217536
                         0 - -4439694546364576038 - 2217588 - 2217536
                         0 - -4439694546364576038 - 2217592 - 2217536
                         4 - -4439694546364576038 - 2217700 - 2217536
                         20 - -4439694546364576038 - 2218000 - 2217536
                         20 - -4439694546364576038 - 2218140 - 2217536
                         55 - -4439694546364576038 - 2218240 - 2217536
                         21 - -4439694546364576038 - 2218464 - 2217536
                         2 - -4439694546364576038 - 2218892 - 2217536
                         1358 - -4439694546364576038 - 2218924 - 2217536
                         127 - -4439694546364576038 - 2224360 - 2217536
                         200 - -4439694546364576038 - 2224896 - 2217536
                         224 - -4439694546364576038 - 2225700 - 2217536
                         0 - -4439694546364576038 - 2227496 - 2217536
                         66 - -4439694546364576038 - 2227512 - 2217536
                         0 - -4439694546364576038 - 2229364 - 2217536
                         0 - -4439694546364576038 - 2229372 - 2217536

                         2021-01-13T05:06:48.960233: Exporting AnimatorOverrideController: bow_11036801
                         2021-01-13T05:06:50.625827: Exporting AnimatorOverrideController: bow_win_11036801
                         2021-01-13T05:06:57.413547: Exporting AnimatorOverrideController: rod_11036101
                         2021-01-13T05:06:57.966324: Exporting AnimatorOverrideController: rod_win_11036101
                         0 - -3667104045530355278 - 2588652 - 2588616 #52209455 i=50 vector=43
                         0 - -3667104045530355278 - 2588656 - 2588616 #52209455 i=82 vector=32
                         0 - -3667104045530355278 - 2588660 - 2588616 #52209455 i=120 vector=38
                         0 - -3667104045530355278 - 2588664 - 2588616 #52209455 i=158 vector=38
                         0 - -3667104045530355278 - 2588668 - 2588616 #52209455 i=196 vector=38
                         0 - -3667104045530355278 - 2588672 - 2588616 #52209455 i=227 vector=31
                         0 - -3667104045530355278 - 2588676 - 2588616 #52209455 i=251 vector=24
                         4 - -3667104045530355278 - 2588784 - 2588616 #52209455 i=48 vector=25
                         20 - -3667104045530355278 - 2589084 - 2588616 #52209455 i=18 vector=4
                         20 - -3667104045530355278 - 2589224 - 2588616 #52209455 i=18 vector=4
                         55 - -3667104045530355278 - 2589324 - 2588616 #52209455 i=98 vector=4
                         21 - -3667104045530355278 - 2589548 - 2588616 #52209455 i=105 vector=7
                         0 - -3667104045530355278 - 2589976 - 2588616 #52209455 i=4 vector=4
                         0 - -3667104045530355278 - 2590000 - 2588616 #52209455 i=8 vector=4
                         0 - -3667104045530355278 - 2590004 - 2588616 #52209455 i=4 vector=4
                         200 - -3667104045530355278 - 2590032 - 2588616 #52209455 i=198 vector=4
                         0 - -3667104045530355278 - 2590836 - 2588616 #52209455 i=204 vector=6
                         0 - -3667104045530355278 - 2590840 - 2588616 #52209455 i=208 vector=4
                         0 - -3667104045530355278 - 2590856 - 2588616 #52209455 i=12 vector=12
                         0 - -3667104045530355278 - 2590860 - 2588616 #52209455 i=18 vector=6
                         0 - -3667104045530355278 - 2590868 - 2588616 #52209455 i=523 vector=19
                         */

                        var list = new List<object>(size);
                        for (var j = 0; j < size; j++)
                        {
                            var tmp = 3;
                            list.Add(ReadValue(vector, reader, ref tmp));
                        }

                        value = list;
                        break;
                    }
                    else //Class
                    {
                        var @class = GetMembers(members, i);
                        i += @class.Count - 1;
                        var obj = new OrderedDictionary();
                        for (var j = 1; j < @class.Count; j++)
                        {
                            var classMember = @class[j];
                            var name = classMember.m_Name;
                            obj[name] = ReadValue(@class, reader, ref j);
                        }

                        value = obj;
                        break;
                    }
                }
            }

            if (align)
                reader.AlignStream();
            return value;
        }

        private static List<TypeTreeNode> GetMembers(List<TypeTreeNode> members, int index)
        {
            var member2 = new List<TypeTreeNode> {members[index]};
            var level = members[index].m_Level;
            for (var i = index + 1; i < members.Count; i++)
            {
                var member = members[i];
                var level2 = member.m_Level;
                if (level2 <= level)
                {
                    return member2;
                }

                member2.Add(member);
            }

            return member2;
        }
    }
}