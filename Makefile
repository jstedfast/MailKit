LIB=MailKit/bin/Debug/lib/MonoTouch/MailKit.iOS.dll

lall:

update-docs: $(LIB)
	mdoc update --out docs/en --delete $(LIB)

