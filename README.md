# VirtuagymRfidReader
Read rfid tag with china reader (NSR122-H) and send it to the checkin client.

# Requirements
1)	Install Com0Com
https://sourceforge.net/projects/com0com/

2)	Install Virtuagym CheckIn Client

# Installation
1) Com0Com add COM4 & COM5
2) VirtuagymClinet select COM4
3) VirtuagymRfidReader write to COM5

Virtuagym RFID Reader -> COM5 -> “data will be forwarded by Com0Com”-> COM4 (Readed by Virtuagym)
