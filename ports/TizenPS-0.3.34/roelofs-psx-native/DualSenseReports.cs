using System;

// Protocol facts cross-checked against SDL's PS5 HID backend and hid-playstation.
// Decode only the two problematic face buttons; never change controller mode.
internal static class DualSenseReports
{
    internal const uint Circle=1u<<8,Triangle=1u<<9,Faces=Circle|Triangle;
    internal static bool Decode(byte[] data,int length,int bus,out uint faces,out string format) {
        faces=0;format=null;
        if(data==null||length<1||length>data.Length)return false;
        int offset;
        if(bus==5 && data[0]==1 && (length==10||length==78)){offset=5;format=length==10?"BT-simple-10":"BT-simple-78";}
        else if(bus==3 && data[0]==1 && length==64){offset=8;format="USB-64";}
        else if(bus==5 && data[0]==0x31 && length==78) {
            uint expected=(uint)(data[74]|data[75]<<8|data[76]<<16|data[77]<<24);
            if(InputCrc(data,74)!=expected)return false;
            offset=9;format="BT-full-78";
        } else return false;
        // Low nibble is the hat, 0..7 directions or 8 neutral.
        if((data[offset]&15)>8)return false;
        if((data[offset]&0x40)!=0)faces|=Circle;
        if((data[offset]&0x80)!=0)faces|=Triangle;
        return true;
    }
    internal static uint InputCrc(byte[] data,int length) {
        uint crc=Update(0xffffffff,0xa1);
        for(int i=0;i<length;i++)crc=Update(crc,data[i]);return ~crc;
    }
    static uint Update(uint crc,byte value) {
        crc^=value;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);return crc;
    }
}

