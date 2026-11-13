namespace CopperCPX;

public partial struct CpxEngine<TMemory> where TMemory : struct, ICpxMemory
{
    private const uint CommentId=15040,CommentPage=15048,CommentLength=15056,CommentScan=15060,CommentEncoded=15064;
    private uint CommentInput=>Arena+15104;
    private uint CommentObject=>Arena+15264;
    public CpxError BeginComment(Wide id,uint latin1,uint length)
    {
        if(length>79||latin1>uint.MaxValue-length||(length!=0&&latin1==0)) return CpxError.InvalidArgument;
        for(uint n=0;n<length;n++) if(memory.Read8(latin1+n)==0) return CpxError.InvalidArgument;
        CpxError error=StartOperation(CpxOperation.SetComment); if(error!=CpxError.None) return error;
        WW(CommentId,id); W(CommentLength,length); Copy(CommentInput,latin1,length); return error;
    }
    private StepResult CommentStep()
    {
        uint obj=CommentObject,p=Page(4); Wide page=RW(CommentPage);
        switch(R(Phase))
        {
            case 0: return LoadObject(RW(CommentId),1);
            case 1:
                if(R(SourcePage)==0||memory.Read32(ObjectRecord+76)==0) return Reject(CpxError.NotFound);
                Copy(obj,ObjectRecord,256); WW(CommentPage,GetWide(obj+48));
                if(!GetWide(obj+48).IsZero) return ReadStaged(GetWide(obj+48),p,2);
                if(R(CommentLength)==0) return Done();
                return EnsureReserve(13,3); // One attribute page plus the 12-page margin.
            case 2:
                if(!ValidPage(p,page)||!ReadableGeneration(p)||Read16(p+4)!=18||!Wide.Equal(GetWide(p+24),RW(CommentId))||!PayloadValid(p)) return Fail(CpxError.Corrupt);
                W(Phase,3); return StepResult.Pending;
            case 3: return BeginTx(6,4);
            case 4:
                if(R(CommentLength)==0) { PutWide(obj+48,default); return ReleaseMetadata(page,9); }
                if(page.IsZero) return TakeMetadata(5);
                W(Phase,6); return StepResult.Pending;
            case 5: WW(CommentPage,RW(MetadataResult)); W(Phase,6); return StepResult.Pending;
            case 6:
                Header(p,18,page,RW(Generation)+1); PutWide(p+24,RW(CommentId));
                Write16(p+64,1); Write16(p+68,1); W(CommentScan,0); W(CommentEncoded,0); W(Phase,7); return StepResult.Pending;
            case 7:
                uint scan=R(CommentScan),encoded=R(CommentEncoded),end=scan+64; if(end>R(CommentLength)) end=R(CommentLength);
                for(;scan<end;scan++)
                {
                    uint c=memory.Read8(CommentInput+scan);
                    if(c>=128) { memory.Write8(p+72+encoded++,(byte)(0xc0|(c>>6))); c=0x80|(c&63); }
                    memory.Write8(p+72+encoded++,(byte)c);
                }
                W(CommentScan,scan); W(CommentEncoded,encoded); if(scan<R(CommentLength)) return StepResult.Pending;
                Write16(p+70,encoded); Write16(p+52,8+encoded); PutWide(obj+48,page); return StagePage(p,page,9);
            case 9: return SaveObject(obj,10);
            case 10: return Call(CpxOperation.CommitTransaction,11);
            case 11: return Done();
            default: return Fail(CpxError.Corrupt);
        }
    }
}
