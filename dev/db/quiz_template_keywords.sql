

CREATE TABLE quiz_template_keywords(
	quiztemplate_id INT,
	keyword NVARCHAR(30),
	CONSTRAINT fgk_tmpl_kwd FOREIGN KEY(quiztemplate_id) REFERENCES quiz_templates(quiztemplate_id) ON DELETE CASCADE ON UPDATE CASCADE,
);


