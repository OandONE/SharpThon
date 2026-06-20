grammar SharpThon;

program: statement+ EOF;

statement
    : variableDecl
    | functionDecl
    | ifStatement
    | forLoop
    | whileLoop
    | tryCatch
    | expr ';'?
    | COMMENT
    ;

variableDecl
    : IDENTIFIER (':' type)? '=' expr ';'?
    ;

functionDecl
    : modifier? 'def' IDENTIFIER '(' params? ')' ('->' type)? block
    ;

ifStatement
    : 'if' '(' expr ')' block ('elif' '(' expr ')' block)* ('else' block)?
    ;

forLoop
    : 'for' '(' IDENTIFIER 'in' expr ')' block
    ;

whileLoop
    : 'while' '(' expr ')' block
    ;

tryCatch
    : 'try' block ('catch' '(' IDENTIFIER IDENTIFIER? ')' block)*
    ;

block: '{' statement* '}';

params: param (',' param)*;
param: IDENTIFIER (':' type)?;

type: 'int' | 'str' | 'bool' | 'float' | 'object';

expr
    : IDENTIFIER '(' exprList? ')'    # functionCall
    | 'Write' '(' exprList? ')'       # writeCall
    | expr op=('*'|'/') expr          # mulDiv
    | expr op=('+'|'-') expr          # addSub
    | expr op=('=='|'!='|'>'|'<'|'>='|'<=') expr # comparison
    | 'return' expr?                  # returnStmt
    | STRING                          # string
    | NUMBER                          # number
    | IDENTIFIER                      # id
    | '(' expr ')'                    # parens
    ;

exprList: expr (',' expr)*;

modifier: 'public' | 'private' | 'static';

COMMENT: '//' ~[\r\n]* -> skip;
STRING: '"' ~["]* '"';
NUMBER: [0-9]+ ('.' [0-9]+)?;
IDENTIFIER: [a-zA-Z_] [a-zA-Z0-9_]*;
WS: [ \t\r\n]+ -> skip;
