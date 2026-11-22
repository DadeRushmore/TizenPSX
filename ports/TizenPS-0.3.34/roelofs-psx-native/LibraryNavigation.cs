using System;

// Index arithmetic is independent of file count: only one page is rendered.
internal static class LibraryNavigation {
 internal const int Columns=6,Rows=3,PageSize=Columns*Rows;
 internal static int Move(int index,int count,string key,bool grid){
  if(count<=0)return 0;
  index=Math.Max(0,Math.Min(index,count-1));
  if(!grid)return key=="Left"?(index+count-1)%count:key=="Right"?(index+1)%count:index;
  if(key=="Left")return (index+count-1)%count;
  if(key=="Right")return (index+1)%count;
  if(key=="Up")return Math.Max(0,index-Columns);
  if(key=="Down")return Math.Min(count-1,index+Columns);
  return index;
 }
 internal static int PageStart(int index)=>Math.Max(0,index)/PageSize*PageSize;
}
