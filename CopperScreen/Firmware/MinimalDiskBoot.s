; CopperScreen original PAL 68000 A500 bootstrap. See docs/engine/MINIMAL_DISK_BOOT.md.
; This is source, assembled once at machine construction by CopperScreenBootAssembler.
; No host traps, CPU hooks, filesystem, scheduler or replacement operating system.
custom equ $dff000
ciaa equ $bfe001
ciab equ $bfd100
exec equ $78000
gfx equ $78800
ioreq equ $79000
state equ $75800
raw equ $1000
decoded equ $5000
boot equ $6800
chipfirst equ $7000
chipend equ $70000
slowfirst equ $c00000
slowend equ $c80000
chipheader equ state+256
slowheader equ state+288
; State: fault word +0, caller +4,
; cylinder +16, track +20, sector +24, remaining +28, destination +32,
; seen sectors +36, control byte +40, slow present +44, forbid +48, disable +50,
; diagnostic detail +52. Memory-header free lists are the allocator authority.
dc.l $400,start
start:
    move.w #$2700,sr
    bset #0,$bfe201
    bclr #0,ciaa
    move.w #$7fff,$dff096
    move.w #$7fff,$dff09a
    move.w #$7fff,$dff09c
    lea $0,a0
    move.l #exception,d0
    move.w #255,d1
vectors:
    move.l d0,(a0)+
    dbra d1,vectors
    ; Reset reads SSP/PC from the ROM overlay. RAM location zero is not an
    ; exception-handler vector; keep the native-boot null-list sentinel clear.
    clr.l $0
    move.l #irq,$64
    move.l #irq,$68
    move.l #irq,$6c
    move.l #irq,$70
    move.l #irq,$74
    move.l #irq,$78
    move.l #irq,$7c
    move.l #supervisor_trap,$80
    move.l #groupzero,$8
    move.l #groupzero,$c
    move.l #exec,$4
    lea state,a0
    move.w #127,d0
clearstate:
    clr.l (a0)+
    dbra d0,clearstate
    move.l #chipfirst,chipheader+16
    clr.l chipfirst
    move.l #chipend-chipfirst,chipfirst+4
    move.l slow_config,state+44
    beq noslow
    move.l #slowfirst,slowheader+16
    clr.l slowfirst
    move.l #slowend-slowfirst,slowfirst+4
noslow:
    lea exec,a0
    lea gfx,a1
    move.w #255,d0
clearlibraries:
    clr.l (a0)+
    clr.l (a1)+
    dbra d0,clearlibraries
    move.l #chipheader,exec+322
    clr.l exec+326
    move.l #chipheader,exec+330
    move.l #exec+326,chipheader
    move.l #exec+322,chipheader+4
    move.b #10,chipheader+8
    move.w #3,chipheader+14
    ; Physical bounds include memory occupied by boot services. The free list
    ; excludes those allocations; takeover code can reclaim RAM after retiring OS use.
    move.l #$800,chipheader+20
    move.l #$80000,chipheader+24
    move.l #chipend-chipfirst,chipheader+28
    tst.l state+44
    beq memoryheadersready
    move.l #slowheader,exec+322
    move.l #slowheader,chipheader+4
    move.l #chipheader,slowheader
    move.l #exec+322,slowheader+4
    move.b #10,slowheader+8
    move.w #5,slowheader+14
    move.l #slowfirst,slowheader+20
    move.l #slowend,slowheader+24
    move.l #slowend-slowfirst,slowheader+28
memoryheadersready:
    lea exec-768,a0
    lea exec_vectors,a1
    move.w #191,d0
copyexec:
    move.l (a1)+,(a0)+
    dbra d0,copyexec
    lea gfx-768,a0
    lea gfx_vectors,a1
    move.w #191,d0
copygfx:
    move.l (a1)+,(a0)+
    dbra d0,copygfx
    move.w #34,exec+20
    move.w #34,gfx+20
    move.w #768,exec+16
    move.w #1024,exec+18
    move.b #9,exec+8
    move.w #768,gfx+16
    move.w #1024,gfx+18
    move.b #9,gfx+8
    move.l #exec+396,exec+392
    clr.l exec+396
    move.l #exec+392,exec+400
    move.l #state+128,exec+276
    move.b #1,state+136
    move.b #2,state+143
    move.l #$7b7fc,state+182
    move.l #$7b000,state+186
    move.l #$7b800,state+190
    move.l #$400,exec+54
    move.l #$100,exec+58
    move.l #$80000,exec+62
    tst.l state+44
    beq noextendedbound
    move.l #slowend,exec+78
noextendedbound:
    move.w #$ffff,exec+294
    move.b #50,exec+530
    move.b #50,exec+531
    lea ioreq,a1
    move.w #23,d0
clearrequest:
    clr.l (a1)+
    dbra d0,clearrequest
    lea ioreq,a1
    move.l #state+64,20(a1)
    move.l #state+80,24(a1)
    move.w #2,28(a1)
    move.l #1024,32(a1)
    move.l #1024,36(a1)
    move.l #boot,40(a1)
    lea exec,a6
    move.b #$ff,$bfd300
    move.b #$ff,ciab
    move.b #$77,ciab
    move.b #$76,ciab
    move.b #$77,ciab
    move.b #$77,state+40
    bsr readtrack
    tst.l d0
    bne boot_invalid
    lea decoded,a0
    lea boot,a1
    move.w #255,d0
copyboot:
    move.l (a0)+,(a1)+
    dbra d0,copyboot
    move.l boot,d0
    andi.l #$ffffff00,d0
    cmpi.l #$444f5300,d0
    bne boot_invalid
    lea boot,a0
    moveq #0,d0
    move.w #255,d1
bootchecksum:
    add.l (a0)+,d0
    bcc bootchecksum_next
    addi.l #1,d0
bootchecksum_next:
    dbra d1,bootchecksum
    cmpi.l #$ffffffff,d0
    bne boot_invalid
    lea ioreq,a1
    move.l #$7b800,a0
    move.l #boot_returned,-(a0)
    move.l a0,usp
    move.l #boot+12,-(sp)
    move.w #$0000,-(sp)
    rte
boot_invalid:
    move.w #$f003,state
    clr.l state+4
    bra fault
irq:
    move.l d0,-(sp)
    move.b $bfed01,d0
    move.b $bfdd00,d0
    move.w #$7fff,$dff09c
    move.l (sp)+,d0
    rte
supervisor:
    dc.w $4e40
    rts
supervisor_trap:
    jmp (a5)
exception:
    move.w #$ffff,state
    move.l 2(sp),state+4
    bra fault
groupzero:
    move.w #$ffff,state
    move.l 10(sp),state+4
    bra fault
boot_returned:
    move.w #$fffe,state
    move.l #boot+12,state+4
fault:
    bra fault
success:
    moveq #0,d0
    rts
findtask:
    move.l a1,d0
    bne success
    move.l #state+128,d0
    rts
addport:
    move.l a1,a0
    lea exec+392,a1
    move.b #4,8(a0)
    bra addtail
addtail_public:
    move.l a0,d0
    move.l a1,a0
    move.l d0,a1
    bra addtail
remport:
    move.l a1,a0
    bra remove
addtail:
    move.l a2,-(sp)
    move.l 8(a1),a2
    move.l a0,(a2)
    move.l a2,4(a0)
    lea 4(a1),a2
    move.l a2,(a0)
    move.l a0,8(a1)
    move.l (sp)+,a2
    rts
remove:
    movem.l a1-a2,-(sp)
    move.l (a0),a1
    move.l 4(a0),a2
    move.l a1,(a2)
    move.l a2,4(a1)
    movem.l (sp)+,a1-a2
    rts
forbid:
    addi.w #1,state+48
    addi.b #1,exec+295
    rts
permit:
    tst.w state+48
    beq success
    subi.w #1,state+48
    subi.b #1,exec+295
    rts
disable:
    addi.w #1,state+50
    addi.b #1,exec+294
    move.w #$4000,$dff09a
    rts
enable:
    tst.w state+50
    beq success
    subi.w #1,state+50
    subi.b #1,exec+294
    tst.w state+50
    bne success
    move.w #$c000,$dff09a
    rts
; AllocMem: first fit, 8-byte granularity, sorted/coalesced FreeMem below.
; Free chunks hold their next pointer and length in the first eight bytes.
allocmem:
    movem.l d1-d7/a0-a5,-(sp)
    tst.l d0
    beq allocfail
    cmpi.l #$80000,d0
    bhi allocfail
    addi.l #7,d0
    andi.l #$fffffff8,d0
    move.l d0,d2
    move.l d1,d7
    move.l d7,d3
    andi.l #6,d3
    cmpi.l #6,d3
    beq allocfail
    move.l d1,d3
    andi.l #$fffefff8,d3
    bne allocfail
    lea chipheader+16,a2
    btst #2,d7
    bne allocslow
allocpool:
    move.l (a2),a0
allocscan:
    move.l a0,d0
    beq allocnextpool
    move.l 4(a0),d3
    cmp.l d2,d3
    bcc allocfound
    move.l a0,a2
    move.l (a0),a0
    bra allocscan
allocnextpool:
    btst #1,d7
    bne allocfail
allocslow:
    lea slowheader+16,a2
    bset #1,d7
    bclr #2,d7
    bra allocpool
allocfound:
    move.l a0,d0
    sub.l d2,d3
    beq allocwhole
    move.l a0,a3
    add.l d2,a3
    move.l (a0),(a3)
    move.l d3,4(a3)
    move.l a3,(a2)
    bra allocclear
allocwhole:
    move.l (a0),(a2)
allocclear:
    btst #16,d1
    beq allocdone
    lsr.l #2,d2
    subi.l #1,d2
alloczero:
    clr.l (a0)+
    dbra d2,alloczero
    swap d2
    subi.w #1,d2
    bcc alloczero_high
    bra allocdone
alloczero_high:
    swap d2
    bra alloczero
allocfail:
    moveq #0,d0
allocdone:
    bsr updatemem
    movem.l (sp)+,d1-d7/a0-a5
    rts
freemem:
    movem.l d0-d3/a0-a3,-(sp)
    move.l a1,d0
    beq freedone
    tst.l d0
    bmi freebad
    move.l d0,d2
    andi.l #7,d2
    bne freebad
    move.l d1,d2
    beq freedone
    addi.l #7,d2
    bcs freebad
    andi.l #$fffffff8,d2
    move.l d0,d3
    add.l d2,d3
    bcs freebad
    lea chipheader+16,a2
    cmpi.l #chipfirst,d0
    bcs freebad
    cmpi.l #chipend,d3
    bls freepool
    tst.l state+44
    beq freebad
    cmpi.l #slowfirst,d0
    bcs freebad
    cmpi.l #slowend,d3
    bhi freebad
    lea slowheader+16,a2
freepool:
    moveq #0,d1
    move.l (a2),a0
freescan:
    move.l a0,d2
    beq freeinsert
    cmp.l d0,d2
    bcc freeinsert
    move.l a0,d1
    move.l a0,a2
    move.l (a0),a0
    bra freescan
freeinsert:
    move.l a0,d2
    beq freeprevious
    cmp.l d3,d2
    bcs freebad
freeprevious:
    tst.l d1
    beq freelink
    move.l d1,a3
    move.l 4(a3),d2
    add.l d1,d2
    cmp.l d0,d2
    bhi freebad
freelink:
    move.l a0,(a1)
    sub.l d0,d3
    move.l d3,4(a1)
    move.l a1,(a2)
    move.l a0,d2
    beq freecoalesceprev
    move.l a1,d2
    add.l d3,d2
    cmp.l a0,d2
    bne freecoalesceprev
    move.l (a0),(a1)
    move.l 4(a0),d2
    add.l d2,4(a1)
freecoalesceprev:
    tst.l d1
    beq freedone
    move.l a1,d2
    move.l 4(a3),d3
    add.l d1,d3
    cmp.l d2,d3
    bne freedone
    move.l (a1),(a3)
    move.l 4(a1),d3
    add.l d3,4(a3)
freedone:
    bsr updatemem
    movem.l (sp)+,d0-d3/a0-a3
    rts
freebad:
    move.w #$f001,state
    move.l 32(sp),state+4
    bra fault
updatemem:
    movem.l d0-d1/a0,-(sp)
    lea chipheader,a0
    bsr sumpool
    lea slowheader,a0
    bsr sumpool
    movem.l (sp)+,d0-d1/a0
    rts
sumpool:
    move.l a1,-(sp)
    move.l 16(a0),a1
    moveq #0,d0
sumpoolnext:
    move.l a1,d1
    beq sumpooldone
    add.l 4(a1),d0
    move.l (a1),a1
    bra sumpoolnext
sumpooldone:
    move.l d0,28(a0)
    move.l (sp)+,a1
    rts
availmem:
    movem.l d1-d3/a0,-(sp)
    moveq #0,d0
    move.l d1,d3
    andi.l #$fffdfff8,d3
    bne availdone
    move.l chipheader+16,a0
    btst #2,d1
    bne availslow
availscan:
    move.l a0,d3
    beq availnext
    move.l 4(a0),d2
    btst #17,d1
    beq availsum
    cmp.l d0,d2
    bls availadvance
    move.l d2,d0
    bra availadvance
availsum:
    add.l d2,d0
availadvance:
    move.l (a0),a0
    bra availscan
availnext:
    btst #1,d1
    bne availdone
availslow:
    bset #1,d1
    bclr #2,d1
    move.l slowheader+16,a0
    bra availscan
availdone:
    movem.l (sp)+,d1-d3/a0
    rts
; Unknown libraries/resources may legitimately be absent; never pretend they work.
openlibrary:
    cmpi.l #34,d0
    bhi success
    move.l a2,-(sp)
    move.l a1,a0
    lea exec_name,a2
    bsr strcmp
    tst.l d0
    beq openexec
    move.l a1,a0
    lea graphics_name,a2
    bsr strcmp
    tst.l d0
    beq opengfx
    moveq #0,d0
    move.l (sp)+,a2
    rts
openexec:
    addi.w #1,exec+32
    move.l #exec,d0
    move.l (sp)+,a2
    rts
opengfx:
    addi.w #1,gfx+32
    move.l #gfx,d0
    move.l (sp)+,a2
    rts
closelibrary:
    cmp.l #exec,a1
    beq closeknown
    cmp.l #gfx,a1
    bne closebad
closeknown:
    tst.w 32(a1)
    beq closebad
    subi.w #1,32(a1)
    rts
closebad:
    move.w #$f005,state
    move.l (sp),state+4
    bra fault
strcmp:
    move.b (a0)+,d0
    cmp.b (a2)+,d0
    bne strcmpdifferent
    tst.b d0
    bne strcmp
    moveq #0,d0
    rts
strcmpdifferent:
    moveq #1,d0
    rts
loadview:
    move.l a1,d0
    bne unsupported_view
    move.w #$0180,$dff096
    clr.l gfx+34
    rts
unsupported_view:
    move.w #$8111,state
    move.l (sp),state+4
    bra fault
waittof:
    move.l d0,-(sp)
waitleave:
    move.l $dff004,d0
    andi.l #$1ff00,d0
    beq waitleave
waitenter:
    move.l $dff004,d0
    andi.l #$1ff00,d0
    bne waitenter
    move.l (sp)+,d0
    rts
opendevice:
    tst.l d0
    bne deviceabsent
    move.l a0,-(sp)
    move.l a2,-(sp)
    lea trackdisk_name,a2
    bsr strcmp
    move.l (sp)+,a2
    move.l (sp)+,a0
    tst.l d0
    bne deviceabsent
    move.l #state+64,20(a1)
    move.l #state+80,24(a1)
    clr.b 31(a1)
    moveq #0,d0
    rts
deviceabsent:
    move.b #$ff,31(a1)
    moveq #-1,d0
    rts
close_device:
    clr.l 20(a1)
    clr.l 24(a1)
    rts
waitio:
    moveq #0,d0
    move.b 31(a1),d0
    ext.w d0
    ext.l d0
    rts
checkio:
    move.l a1,d0
    rts
; Synchronous DoIO and completed WaitIO only. No scheduler/message-port
; completion protocol: SendIO/CheckIO remain explicit unsupported vectors.
doio:
    movem.l d1-d7/a0-a6,-(sp)
    clr.b 31(a1)
    clr.l 32(a1)
    cmpi.l #state+64,20(a1)
    bne io_baddevice
    cmpi.l #state+80,24(a1)
    bne io_baddevice
    cmpi.w #2,28(a1)
    beq readrequest
    cmpi.w #9,28(a1)
    beq motorrequest
    cmpi.w #4,28(a1)
    beq io_ok
    cmpi.w #5,28(a1)
    beq io_ok
    move.w #$f002,state
    moveq #0,d0
    move.w 28(a1),d0
    move.l d0,state+52
    move.l 56(sp),state+4
    bra fault
readrequest:
    move.l 36(a1),d0
    move.l 44(a1),d1
    move.l d0,d2
    or.l d1,d2
    andi.l #511,d2
    bne io_badlength
    move.l d0,d2
    add.l d1,d2
    bcs io_badlength
    cmpi.l #901120,d2
    bhi io_badlength
    move.l 40(a1),d3
    move.l d3,d2
    andi.l #1,d2
    bne io_badaddress
    move.l d3,d2
    add.l d0,d2
    bcs io_badaddress
    cmpi.l #chipfirst,d3
    bcc io_chipaddress
    cmpi.l #boot,d3
    bcs io_badaddress
    cmpi.l #boot+1024,d2
    bls io_addressok
    bra io_badaddress
io_chipaddress:
    cmpi.l #$80000,d2
    bhi io_slowaddress
    cmpi.l #state,d2
    bls io_addressok
    cmpi.l #$7c000,d3
    bcc io_addressok
    move.w #$f004,state
    move.l 56(sp),state+4
    move.l d3,state+52
    bra fault
io_slowaddress:
    tst.l state+44
    beq io_badaddress
    cmpi.l #slowfirst,d3
    bcs io_badaddress
    cmpi.l #slowend,d2
    bhi io_badaddress
io_addressok:
    move.l d3,state+32
    lsr.l #8,d0
    lsr.l #1,d0
    move.l d0,state+28
    lsr.l #8,d1
    lsr.l #1,d1
    move.l d1,state+24
    move.l a1,a6
readnexttrack:
    tst.l state+28
    beq io_ok
    move.l state+24,d0
    divu #11,d0
    andi.l #$ffff,d0
    move.l d0,state+20
    bsr readtrack
    tst.l d0
    bne io_readerror
copysectors:
    move.l state+24,d0
    divu #11,d0
    swap d0
    andi.l #$ffff,d0
    lsl.l #8,d0
    lsl.l #1,d0
    lea decoded,a0
    add.l d0,a0
    move.l state+32,a2
    move.w #127,d0
copysector:
    move.l (a0)+,(a2)+
    dbra d0,copysector
    move.l a2,state+32
    addi.l #512,32(a6)
    addi.l #1,state+24
    subi.l #1,state+28
    beq io_ok
    move.l state+24,d0
    divu #11,d0
    swap d0
    tst.w d0
    bne copysectors
    bra readnexttrack
io_badlength:
    moveq #-4,d0
    bra io_error
io_baddevice:
    moveq #-1,d0
    bra io_error
io_badaddress:
    moveq #-5,d0
    bra io_error
io_readerror:
    moveq #20,d0
io_error:
    move.b d0,31(a1)
    bra io_done
io_ok:
    moveq #0,d0
io_done:
    movem.l (sp)+,d1-d7/a0-a6
    rts
motorrequest:
    moveq #0,d0
    move.b state+40,d0
    btst #7,d0
    bne motorwasoff
    move.l #1,32(a1)
motorwasoff:
    move.b #$ff,ciab
    move.b #$f7,d0
    tst.l 36(a1)
    beq motorset
    move.b #$77,d0
motorset:
    move.b d0,ciab
    move.b d0,state+40
    bra io_ok
; Re-home on every track read: disk code may have moved the head between calls.
; No cache survives DoIO, so disk swaps and direct guest disk writes cannot stale it.
readtrack:
    move.b #$ff,ciab
    move.b #$77,ciab
    move.b #$77,state+40
    move.w #83,d7
home:
    btst #4,ciaa
    beq homed
    bsr step
    dbra d7,home
    bra trackbad
homed:
    move.l state+20,d7
    lsr.l #1,d7
    beq sought
    move.b #$75,state+40
seek:
    bsr step
    subi.w #1,d7
    bne seek
sought:
    move.b state+40,d0
    btst #0,state+23
    beq sidezero
    bclr #2,d0
sidezero:
    move.b d0,ciab
    move.b d0,state+40
    move.l #200000,d7
ready:
    btst #5,ciaa
    beq diskready
    subi.l #1,d7
    bne ready
    bra trackbad
diskready:
    move.w #$4000,$dff024
    move.w #$7f00,$dff09e
    move.w #$9500,$dff09e
    move.w #$4489,$dff07e
    move.l #raw,$dff020
    move.w #$0002,$dff09c
    move.w #$8210,$dff096
    move.w #$a000,$dff024
    move.w #$a000,$dff024
    move.l #200000,d7
diskwait:
    btst #1,$dff01f
    bne diskdone
    subi.l #1,d7
    bne diskwait
    move.w #$4000,$dff024
    bra trackbad
diskdone:
    move.w #$4000,$dff024
    move.w #$0010,$dff096
    move.w #$0002,$dff09c
    clr.w state+36
    lea raw,a0
scan:
    cmp.l #raw+16384-1084,a0
    bhi trackbad
    cmpi.w #$4489,(a0)+
    bne scan
    cmpi.w #$4489,(a0)
    beq scan
    move.l (a0),d0
    move.l 4(a0),d1
    andi.l #$55555555,d0
    andi.l #$55555555,d1
    add.l d0,d0
    or.l d1,d0
    move.l d0,d3
    lsr.l #8,d3
    andi.l #$ff,d3
    cmpi.w #10,d3
    bhi scan
    move.l d0,d2
    lsr.l #8,d2
    lsr.l #8,d2
    cmp.b state+23,d2
    bne scan
    lsr.l #8,d2
    cmpi.b #$ff,d2
    bne scan
    moveq #0,d2
    moveq #9,d4
    move.l a0,a2
headerxor:
    move.l (a2)+,d0
    eor.l d0,d2
    dbra d4,headerxor
    andi.l #$55555555,d2
    move.l 40(a0),d0
    move.l 44(a0),d1
    andi.l #$55555555,d0
    andi.l #$55555555,d1
    add.l d0,d0
    or.l d1,d0
    cmp.l d0,d2
    bne scan
    lea 56(a0),a2
    lea 568(a0),a3
    move.l d3,d0
    lsl.l #8,d0
    lsl.l #1,d0
    lea decoded,a4
    add.l d0,a4
    moveq #0,d2
    move.w #127,d4
decode:
    move.l (a2)+,d0
    move.l (a3)+,d1
    andi.l #$55555555,d0
    andi.l #$55555555,d1
    eor.l d0,d2
    eor.l d1,d2
    add.l d0,d0
    or.l d1,d0
    move.l d0,(a4)+
    dbra d4,decode
    move.l 48(a0),d0
    move.l 52(a0),d1
    andi.l #$55555555,d0
    andi.l #$55555555,d1
    add.l d0,d0
    or.l d1,d0
    cmp.l d0,d2
    bne scan
    move.w state+36,d0
    bset d3,d0
    move.w d0,state+36
    cmpi.w #$7ff,d0
    bne scan
    moveq #0,d0
    rts
trackbad:
    moveq #-1,d0
    rts
step:
    move.b state+40,d0
    bclr #0,d0
    move.b d0,ciab
    bset #0,d0
    move.b d0,ciab
    move.w #3000,d6
stepdelay:
    dbra d6,stepdelay
    rts
exec_name:
    dc.b $65,$78,$65,$63,$2e,$6c,$69,$62,$72,$61,$72,$79,0,0
graphics_name:
    dc.b $67,$72,$61,$70,$68,$69,$63,$73,$2e,$6c,$69,$62,$72,$61,$72,$79,0,0
trackdisk_name:
    dc.b $74,$72,$61,$63,$6b,$64,$69,$73,$6b,$2e,$64,$65,$76,$69,$63,$65,0,0
slow_config:
    dc.l 0
