using NewTek;
using System.Runtime.InteropServices;

internal class Program
{
    private static void Main(string[] args)
    {
        var digits_design = new Dictionary<char, string[]>()
        {

            {  '0', new[]{"01110",
          "10001",
          "10001",
          "10001",
          "10001",
          "10001",
          "01110",
          "00000"} },
             {'1', new string[]{"00100",
        "01100",
        "00100",
        "00100",
        "00100",
        "00100",
        "01110",
        "00000"}},

 {'2', new string[]{"01110",
        "10001",
        "00001",
        "00010",
        "00100",
        "01000",
        "11111",
        "00000"}},

 {'3', new string[]{"01110",
        "10001",
        "00001",
        "00110",
        "00001",
        "10001",
        "01110",
        "00000"}},

 {'4', new string[]{"00010",
        "00110",
        "01010",
        "10010",
        "11111",
        "00010",
        "00010",
        "00000"}},

 {'5', new string[]{"11111",
        "10000",
        "11110",
        "00001",
        "00001",
        "10001",
        "01110",
        "00000"}},

 {'6', new string[]{"00110",
        "01000",
        "10000",
        "11110",
        "10001",
        "10001",
        "01110",
        "00000"}},

 {'7', new string[]{"11111",
        "00001",
        "00010",
        "00100",
        "01000",
        "01000",
        "01000",
        "00000"}},

 {'8', new string[]{"01110",
        "10001",
        "10001",
        "01110",
        "10001",
        "10001",
        "01110",
        "00000"}},

 {'9', new string[]{"01110",
        "10001",
        "10001",
        "01111",
        "00001",
        "00010",
        "01100",
        "00000"}
            }

        };

        var frames = new List<NDIlib.video_frame_v2_t>();
        var senders = new List<nint>();

        var input = string.Empty;

        Console.WriteLine("Do you want an override name? Hit enter to skip.");
        var overrideName = Console.ReadLine();

        var sourceCount = 0;
        while(sourceCount <= 0 || sourceCount > 128)
        {
            Console.WriteLine("How many dummy sources do you want? Max 128");
            int.TryParse(Console.ReadLine(), out sourceCount);
        }

        if (!string.IsNullOrEmpty(overrideName))
        {
            var envDetails = $$"""{"ndi": { "machinename": "{{overrideName}}" } }""";
            File.WriteAllText("ndi-config.v1.json", envDetails);
            var pathToEnv = Path.Combine(Environment.CurrentDirectory, "ndi-config.v1.json");
            Environment.SetEnvironmentVariable("NDI_CONFIG_DIR", Environment.CurrentDirectory);

            Console.WriteLine($"WARNING: Machine name has been overridden to {overrideName}");
        }

        Console.WriteLine("Do you want to specify a custom NDI source prefix name?");
        var sourcePrefixName = Console.ReadLine();

        if (string.IsNullOrEmpty(sourcePrefixName))
        {
            sourcePrefixName = "Fake Source";
        }

        NDIlib.initialize();

        for (var i = 0; i < sourceCount; i++)
        {
            var frameBuffer = Marshal.AllocHGlobal(64 * 64 * 4);
            var numDotsFilledIn = i + 1;

            var stringToDisplay = numDotsFilledIn.ToString();

            unsafe
            {
                var ptr = (byte*)frameBuffer.ToPointer();
                for (var p = 0; p < 64 * 64 * 4; p+=4)
                {
                    ptr[p] = 0x44;
                    ptr[p+1] = 0x0;
                    ptr[p+2] = 0x0;
                    ptr[p+3] = 0xFF;
                }


                for(var c = 0; c < stringToDisplay.Length; c++)
                {
                    var charToDisplay = stringToDisplay[c];
                    var offset = c * 7;

                    var charArray = digits_design[charToDisplay];

                    for(var y = 0; y < charArray.Length; y++)
                    {
                        var letterRow = charArray[y];

                        for(var x = 0; x < letterRow.Length; x++)
                        {
                            var stride = ((y + 5) * 64) + x + offset + 4;

                            var toSet = (byte)(letterRow[x] == '1' ? 0xFF : 0x0);

                            if(toSet == 0x0)
                            {
                                continue;
                            }

                            var pixelOffset = stride * 4;
                            ptr[pixelOffset] = toSet;
                            ptr[pixelOffset + 1] = toSet;
                            ptr[pixelOffset + 2] = toSet;
                            ptr[pixelOffset + 3] = toSet;
                        }
                    }
                }

                //for (var p = 0; p < numDotsFilledIn * 4; p+=4)
                //{
                //    ptr[p] = 0xFF;
                //    ptr[p + 1] = 0xFF;
                //    ptr[p + 2] = 0x0;
                //    ptr[p + 3] = 0xFF;
                //}
            }

            var frame = new NDIlib.video_frame_v2_t()
            {
                FourCC = NDIlib.FourCC_type_e.FourCC_type_BGRA,
                frame_format_type = NDIlib.frame_format_type_e.frame_format_type_progressive,
                frame_rate_N = 60,
                frame_rate_D = 1,
                xres = 64,
                yres = 64,
                picture_aspect_ratio = 1,
                line_stride_in_bytes = 4 * 64,
                p_data = frameBuffer,
                p_metadata = nint.Zero,
                timecode = NDIlib.send_timecode_synthesize,
                timestamp = 0
            };

            frames.Add(frame);

            var senderPtrName = NewTek.NDI.UTF.StringToUtf8($"{sourcePrefixName} {numDotsFilledIn}");
            var senderDetails = new NDIlib.send_create_t
            {
                p_ndi_name = senderPtrName,
                clock_video = true
            };

            var senderPtr = NDIlib.send_create(ref senderDetails);
            Marshal.FreeHGlobal(senderPtrName);

            senders.Add(senderPtr);
        }


        Console.WriteLine("Sending. Hit Q to quit.");
        var key = new ConsoleKeyInfo();
        while (input != "!q")
        {
            var keyAvailable = Console.KeyAvailable;

            if (keyAvailable)
            {
                key = Console.ReadKey();
            }

            if(key.Key == ConsoleKey.Q)
            {
                Console.WriteLine("Q key pressed - quitting.");
                break;
            }
            else
            {
                for (var i = 0; i < frames.Count; i++)
                {
                    var frame = frames[i];
                    var sender = senders[i];

                    NDIlib.send_send_video_v2(sender, ref frame);
                }

                Thread.Sleep(500);
            }

        }

        foreach (var item in frames)
        {
            Marshal.FreeHGlobal(item.p_data);
        }

        foreach (var item in senders)
        {
            NDIlib.send_destroy(item);
        }

        frames.Clear();
        senders.Clear();
    }
}