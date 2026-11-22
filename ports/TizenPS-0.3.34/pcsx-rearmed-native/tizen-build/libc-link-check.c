#include <sys/stat.h>
int check(const char *p,struct stat *s){return stat(p,s); }
