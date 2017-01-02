#GenericTrainer
provides generic process memory read/write operations

#How to

This application is rather simple to use.
While you can use it to find values in a process (via monitoring),
it is not the purpose of this.
This tool assumes you already know the address(es) you want to read/write.
This application allows you to generically set values in a process.

##Call

    GenericTrainer.exe <[/L:[Path]]ProcessName|ProcessId> [/O]
                       [[+][#][Type:]Region[=Value]]

    ProcessName   - Name of the Process. Must end in '.exe' to not be treated as ID
    ProcessId     - ID of the Process
    /L            - Launch the process specified. If the name/path contains spaces, enclose the whole argument with double quotes (including '/L:'). The process is always launched, regardless if an identical instance is already running.
    Path          - If /L is specified, you can use the full/relative path to launch.
    /O            - Run only once and exit. Default is to loop until the target process exits or CTRL+C is hit.
    +             - If present, the region specifies an offset from the base
    #             - If present, the region is treated as a pointer to the location that should be read/written instead. Can be used multiple times for 'stacked' pointers (A pointer list).
    Region        - Memory address to read/write. Hexadecimal
    Type          - Value type. Supported: L=Int64, I=Int32, S=Int16, B=Int8. This argument defaults to 'I'
    Value         - The value to set. Prefix with '0x' for hexadecimal. You can use negative hexadecimal numbers with '-0x'. You can use a region reference here.

##Notes on arguments
- If only the process name or id is supplied, it will show some information. This always exits immediately (/O is not required). Warning! If you use /L with this, it will launch the Process and leave it running 'as-is'.
- If no value is specified for a region, that region is just shown.
- Multiple regions can be specified to show/update multiple values at once.
- If a process name is specified and multiple names are found, it will take the one with the highest process ID.
- The value can be a region reference (see example below) to set it to the same value a read/written region has. Regions are assigned incremental IDs, starting at 1.

##Example call

    GenericTrainer.exe /L:test.exe +##I:402F #B:85B3E=0x10 +S:0=R1

- This will launch 'test.exe'
- It will monitor the value of the address of the address of the base address+0x402F as integer. *This is R1*
- It will write the byte value 16 to the address '85B3E' points to. *This is R2*
- It will Write the value from R1 to the base address of the process. This also converts the value from int to short. It will cut off excessive bits without rounding or converting. *This is R3*

