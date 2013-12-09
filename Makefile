LIB=MailKit/bin/Debug/lib/MonoTouch/MailKit.iOS.dll

all:
	mdoc assemble --out=MailKit docs/en

update-docs: $(LIB)
	mdoc update --out docs/en --delete $(LIB)

